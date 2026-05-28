using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Central hub for real-time notifications + presence broadcasting.
///
/// On connect:
///   - Adds the connection to the "user:{userId}" group (server-initiated
///     pushes from other services target this group).
///   - Registers presence with <see cref="IPresenceTracker"/>; if this is the
///     user's first connection, broadcasts "UserOnline" to every accepted friend.
///
/// On disconnect:
///   - Removes the connection from the user group.
///   - When the user's last connection drops, broadcasts "UserOffline" to friends.
///
/// Server code can push at any time via <c>IHubContext&lt;NotificationHub&gt;</c>
/// (typically wrapped by <see cref="INotificationService"/>).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly IPresenceTracker _presence;
    private readonly OmnijoyDbContext _db;

    public NotificationHub(IPresenceTracker presence, OmnijoyDbContext db)
    {
        _presence = presence;
        _db       = db;
    }

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

            var becameOnline = await _presence.ConnectedAsync(userId, Context.ConnectionId);
            if (becameOnline)
                await BroadcastPresenceToFriendsAsync(userId, online: true);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

            var becameOffline = await _presence.DisconnectedAsync(userId, Context.ConnectionId);
            if (becameOffline)
                await BroadcastPresenceToFriendsAsync(userId, online: false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client-callable: returns presence for a batch of user IDs without going
    /// through the REST API (handy for the conversation list on chat open).
    /// </summary>
    public async Task<object> GetPresence(string[] userIds)
    {
        var parsed = userIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToArray();

        var presence = await _presence.GetPresenceAsync(parsed);
        return presence;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends "UserOnline" / "UserOffline" to every accepted friend of <paramref name="userId"/>.
    /// </summary>
    private async Task BroadcastPresenceToFriendsAsync(Guid userId, bool online)
    {
        var friendIds = await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        if (friendIds.Count == 0) return;

        var groups = friendIds.Select(id => $"user:{id}").ToArray();
        var eventName = online ? "UserOnline" : "UserOffline";
        await Clients.Groups(groups).SendAsync(eventName, new
        {
            userId = userId.ToString(),
            at     = DateTime.UtcNow,
        });
    }
}
