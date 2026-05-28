using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Hub for live streaming events: stream start/end notifications,
/// viewer count updates, and live chat during a stream.
/// </summary>
[Authorize]
public class LiveHub : Hub
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

    /// <summary>Viewer joins a live stream room to receive chat + viewer count updates.</summary>
    public async Task JoinStream(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream:{streamId}");
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerJoined", streamId);
    }

    /// <summary>Viewer leaves a live stream room.</summary>
    public async Task LeaveStream(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream:{streamId}");
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerLeft", streamId);
    }

    /// <summary>Send a chat message to all viewers of a stream.</summary>
    public async Task SendLiveChat(string streamId, string message)
    {
        var userId = Context.UserIdentifier;
        await Clients.Group($"stream:{streamId}")
            .SendAsync("LiveChatMessage", streamId, userId, message, DateTime.UtcNow);
    }
}
