using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Hub for real-time messenger / chat functionality.
/// Clients join conversation groups by conversationId.
/// All messages are delivered via server push — no polling.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Join a specific conversation group to receive messages in real-time.</summary>
    public async Task JoinConversation(string conversationId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

    /// <summary>Leave a conversation group.</summary>
    public async Task LeaveConversation(string conversationId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

    /// <summary>Client sends typing indicator to conversation partners.</summary>
    public async Task SendTyping(string conversationId)
    {
        var userId = Context.UserIdentifier;
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .SendAsync("UserTyping", conversationId, userId);
    }
}
