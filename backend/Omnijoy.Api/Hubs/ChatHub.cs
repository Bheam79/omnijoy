using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Metrics;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Hub for real-time messenger / chat functionality.
///
/// Client flow:
///   1. Connect  → server adds connection to "user:{userId}" group
///   2. Open chat window → client calls JoinConversation
///   3. Server pushes ReceiveMessage / MessageDeleted to "conversation:{id}" group
///   4. Client calls LeaveConversation when closing a window
///   5. Client calls SendTyping to broadcast a typing indicator
///
/// UserOnline / UserOffline presence events are broadcast to all other
/// connected clients on connect/disconnect.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        SignalRMetrics.Increment("chat");

        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            // Broadcast presence to everyone else
            await Clients.Others.SendAsync("UserOnline", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        SignalRMetrics.Decrement("chat");

        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
            await Clients.Others.SendAsync("UserOffline", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific conversation group to receive real-time messages.
    /// Call this when the user opens a chat window.
    /// </summary>
    public async Task JoinConversation(string conversationId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

    /// <summary>Leave a conversation group (called when closing a chat window).</summary>
    public async Task LeaveConversation(string conversationId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

    /// <summary>
    /// Broadcast a typing indicator to the other participants in the conversation.
    /// The server echoes it only to *other* members of the group.
    /// </summary>
    public async Task SendTyping(string conversationId)
    {
        var userId = Context.UserIdentifier;
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .SendAsync("UserTyping", conversationId, userId);
    }
}
