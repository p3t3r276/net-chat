using System.Text.Json;
using OurSpace.API.Models;
using OurSpace.API.Services;
using StackExchange.Redis;

namespace OurSpace.API.BackgroundServices;

public class MessageStreamConsumerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageStreamConsumerService> _logger;

    private const string ChatStreamName = "chat_messages_stream";
    private const string ConsumerGroupName = "chat_persistence_group";
    private const string ConsumerName = "chat_persistence_consumer";

    public MessageStreamConsumerService(
        IConnectionMultiplexer redis,
        IServiceProvider serviceProvider,
        ILogger<MessageStreamConsumerService> logger)
    {
        _redis = redis;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageStreamConsumerService running.");

        var db = _redis.GetDatabase();

        // Ensure the consumer group exists. Create it if it doesn't.
        // MKSTREAM creates the stream if it doesn't exist.
        try
        {
            await db.StreamCreateConsumerGroupAsync(ChatStreamName, ConsumerGroupName, StreamPosition.NewMessages);
            _logger.LogInformation("Redis Stream consumer group '{ConsumerGroupName}' created or already exists.", ConsumerGroupName);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            _logger.LogInformation("Redis Stream consumer group '{ConsumerGroupName}' already exists.", ConsumerGroupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Redis Stream consumer group '{ConsumerGroupName}'.", ConsumerGroupName);
            // Depending on your error handling strategy, you might want to stop the service here.
            return;
        }

        // Start consuming messages
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // XREADGROUP reads messages from the stream for a specific consumer group.
                // BLOCK 1000 means wait up to 1000ms if no messages are available.
                // COUNT 1 means read one message at a time for simpler processing.
                // ">" means read new messages that haven't been delivered to this consumer group yet.
                // If you want to process pending messages (e.g., after a restart), you'd use "0" or a specific ID.
                var streamEntries = await db.StreamReadGroupAsync(
                    ChatStreamName,
                    ConsumerGroupName,
                    ConsumerName,
                    StreamPosition.NewMessages, // Read new messages
                    count: 1 // Process one message at a time
                );

                if (streamEntries != null && streamEntries.Length != 0)
                {
                    foreach (var entry in streamEntries)
                    {
                        try
                        {
                            // Each stream entry has an ID and a collection of NameValueEntry (fields).
                            // We stored the whole message JSON in a field named "data".
                            var messageJson = entry.Values.FirstOrDefault(x => x.Name == "data").Value;
                            if (messageJson.HasValue)
                            {
                                var message = JsonSerializer.Deserialize<Message>(messageJson!);
                                if (message != null)
                                {
                                    // Use a new scope for MessageService to ensure proper DbContext lifetime
                                    using (var scope = _serviceProvider.CreateScope())
                                    {
                                        var messageService = scope.ServiceProvider.GetRequiredService<MessageService>();
                                        await messageService.AddMessageAsync(message);
                                    }

                                    // Acknowledge the message in the stream after successful processing
                                    await db.StreamAcknowledgeAsync(ChatStreamName, ConsumerGroupName, entry.Id);
                                    _logger.LogInformation("Message ID {MessageId} processed and acknowledged.", entry.Id);
                                }
                                else
                                {
                                    _logger.LogWarning("Could not deserialize message from stream entry ID {MessageId}.", entry.Id);
                                    // Potentially acknowledge or move to a dead-letter stream if deserialization consistently fails
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Stream entry ID {MessageId} has no 'data' field.", entry.Id);
                                // Acknowledge to prevent reprocessing of malformed messages if appropriate
                                await db.StreamAcknowledgeAsync(ChatStreamName, ConsumerGroupName, entry.Id);
                            }
                        }
                        catch (Exception entryEx)
                        {
                            _logger.LogError(entryEx, "Error processing stream entry ID {MessageId}. Message will remain in pending list.", entry.Id);
                            // The message is NOT acknowledged, so it will be retried by this or another consumer.
                            // You might want to implement a retry count and move to a dead-letter stream after too many failures.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from Redis Stream '{StreamName}'. Retrying in 5 seconds...", ChatStreamName);
                await Task.Delay(5000, stoppingToken); // Wait before retrying stream read
            }
        }

        _logger.LogInformation("MessageStreamConsumerService stopped.");
    }
}