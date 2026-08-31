using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Metrics;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Hub for real-time feed updates.
/// When a user creates a post, the server pushes a NewPost event to all
/// connected friends/followers — no polling required.
/// Clients may also subscribe to individual post groups to receive
/// real-time post and comment reaction count updates
/// (<c>ReactionCountsUpdated</c> and <c>CommentReactionCountsUpdated</c> events).
/// </summary>
[Authorize]
public class FeedHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        SignalRMetrics.Increment("feed");

        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        SignalRMetrics.Decrement("feed");

        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe this connection to real-time updates for a specific post
    /// (e.g. reaction count changes).  Clients call this when a post is visible
    /// on screen; call <see cref="UnsubscribeFromPost"/> when it leaves the viewport.
    /// </summary>
    public async Task SubscribeToPost(Guid postId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"post:{postId}");

    /// <summary>Unsubscribe from per-post updates.</summary>
    public async Task UnsubscribeFromPost(Guid postId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"post:{postId}");
}
