using System.Text.Json;
using OurSpace.API.Models;
using StackExchange.Redis;

namespace OurSpace.API.BackgroundServices;

public class RedisStreamProducerService(
    IConnectionMultiplexer redis,
    ILogger<RedisStreamProducerService> logger)
{
    private const string ChatStreamName = "chat_messages_stream";

    /// <summary>
    /// Publishes a chat message to the Redis Stream.
    /// This acts as the "outbox" for messages destined for persistence.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    public async Task PublishMessageToStreamAsync(Message message)
    {
        var db = redis.GetDatabase();
        var messageJson = JsonSerializer.Serialize(message);

        try
        {
            // XADD adds an entry to a stream. "*" generates a new ID.
            // We're storing the entire message as a single field "data".
            await db.StreamAddAsync(ChatStreamName, [new NameValueEntry("data", messageJson)]);
            logger.LogInformation("Message published to Redis Stream '{StreamName}': {Content}", ChatStreamName, message.Content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing message to Redis Stream '{StreamName}': {ErrorMessage}", ChatStreamName, ex.Message);
            // Depending on criticality, you might want to re-queue or log to a dead-letter system here.
            throw;
        }
    }
}
