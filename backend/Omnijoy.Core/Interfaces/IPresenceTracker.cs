using Omnijoy.Core.DTOs.Notifications;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Tracks online/offline status per user using SignalR connection lifecycle.
///
/// Implementation notes:
///   - In-memory by default (ConcurrentDictionary keyed by userId).
///   - For multi-instance deployments swap in a Redis-backed implementation
///     behind the same interface. The data model is intentionally trivial.
///   - A user is "online" while they have at least one open hub connection.
///     When the last connection drops, LastSeenAt is recorded and the user
///     becomes "offline" after a small grace period (handled by the consumer).
/// </summary>
public interface IPresenceTracker
{
    /// <summary>
    /// Records a new hub connection for the user. Returns <c>true</c> when this
    /// is the user's first connection (transitioning offline → online).
    /// </summary>
    Task<bool> ConnectedAsync(Guid userId, string connectionId);

    /// <summary>
    /// Removes a hub connection for the user. Returns <c>true</c> when this
    /// was the user's last connection (transitioning online → offline).
    /// </summary>
    Task<bool> DisconnectedAsync(Guid userId, string connectionId);

    /// <summary>Returns <c>true</c> if the user has at least one active connection.</summary>
    Task<bool> IsOnlineAsync(Guid userId);

    /// <summary>Returns presence snapshots for a batch of user IDs.</summary>
    Task<UserPresenceDto[]> GetPresenceAsync(IEnumerable<Guid> userIds);

    /// <summary>Returns the IDs of all currently-online users (use for cross-instance broadcast).</summary>
    Task<Guid[]> GetOnlineUsersAsync();
}
