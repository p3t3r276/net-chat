using Microsoft.AspNetCore.SignalR;
using OurSpace.API.Models;
using OurSpace.API.Services;

namespace OurSpace.API.Hubs;

public class ChatHub(MessageService messageService, ILogger<ChatHub> logger)
    : Hub
{
    /// <summary>
    /// Handles incoming chat messages from clients.
    /// Saves the message to the database and broadcasts it to all connected clients.
    /// </summary>
    /// <param name="user">The username of the sender.</param>
    /// <param name="messageContent">The content of the message.</param>
    public async Task SendMessage(string user, string messageContent)
    {
        logger.LogInformation("Received message from {User}: {Content}", user, messageContent);

        // Basic validation
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(messageContent))
        {
            logger.LogWarning("Invalid message received: User or Content is empty.");
            // Optionally send an error back to the caller
            await Clients.Caller.SendAsync("ReceiveSystemMessage", "Error: User and message content cannot be empty.");
            return;
        }

        var message = new Message
        {
            UserName = user,
            Content = messageContent,
            Timestamp = DateTime.UtcNow // Ensure server-side timestamp for consistency
        };

        try
        {
            // Save message to SQLite
            await messageService.AddMessageAsync(message);

            // Broadcast message to all connected clients
            // The Redis backplane ensures this message is sent to clients connected to any server instance
            await Clients.All.SendAsync("ReceiveMessage", message.UserName, message.Content, message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            logger.LogInformation("Message broadcasted: {User} - {Content}", message.UserName, message.Content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process and broadcast message: {User} - {Content}", user, messageContent);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", "Error sending message. Please try again.");
        }
    }

    /// <summary>
    /// Called when a new client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        // Optionally send recent messages to the newly connected client
        var recentMessages = await messageService.GetRecentMessagesAsync();
        foreach (var msg in recentMessages.OrderBy(m => m.Timestamp)) // Ensure chronological order for display
        {
            await Clients.Caller.SendAsync("ReceiveMessage", msg.UserName, msg.Content, msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}. Exception: {Exception}", Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }
}