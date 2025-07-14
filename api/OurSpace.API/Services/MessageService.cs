using Microsoft.EntityFrameworkCore;
using OurSpace.API.Data;
using OurSpace.API.Models;

namespace OurSpace.API.Services;

public class MessageService(
    AppDbContext context,
    ILogger<MessageService> logger)
{
    /// <summary>
    /// Adds a new message to the database.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <returns>The added message with its generated ID.</returns>
    public async Task<Message> AddMessageAsync(Message message)
    {
        try
        {
            context.Messages.Add(message);
            await context.SaveChangesAsync();
            logger.LogInformation("Message from {UserName} saved to DB. Content: {Content}", message.UserName, message.Content);
            return message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving message to database: {ErrorMessage}", ex.Message);
            throw; // Re-throw to propagate the error if necessary
        }
    }

    /// <summary>
    /// Retrieves a specified number of recent messages from the database.
    /// </summary>
    /// <param name="count">The number of messages to retrieve.</param>
    /// <returns>A list of recent messages, ordered by timestamp descending.</returns>
    public async Task<List<Message>> GetRecentMessagesAsync(int count = 50)
    {
        try
        {
            return await context.Messages
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving recent messages from database: {ErrorMessage}", ex.Message);
            return []; // Return empty list on error
        }
    }
}
