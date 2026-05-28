using System.Collections.Concurrent;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Single-instance in-memory presence tracker.
///
/// Stores: userId → { connectionIds, lastSeenAt }.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// A user is online while they have at least one tracked connectionId; the
/// LastSeenAt timestamp is bumped on disconnect so callers can show "Last seen 5m ago".
///
/// Replace with a Redis implementation behind <see cref="IPresenceTracker"/>
/// when scaling beyond a single backend instance.
/// </summary>
public class InMemoryPresenceTracker : IPresenceTracker
{
    private sealed class UserPresence
    {
        public ConcurrentDictionary<string, byte> ConnectionIds { get; } = new(StringComparer.Ordinal);
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<Guid, UserPresence> _users = new();

    public Task<bool> ConnectedAsync(Guid userId, string connectionId)
    {
        var entry = _users.GetOrAdd(userId, _ => new UserPresence());
        bool wasEmpty;
        lock (entry)
        {
            wasEmpty = entry.ConnectionIds.IsEmpty;
            entry.ConnectionIds[connectionId] = 0;
            entry.LastSeenAt = DateTime.UtcNow;
        }
        return Task.FromResult(wasEmpty);
    }

    public Task<bool> DisconnectedAsync(Guid userId, string connectionId)
    {
        if (!_users.TryGetValue(userId, out var entry))
            return Task.FromResult(false);

        bool nowEmpty;
        lock (entry)
        {
            entry.ConnectionIds.TryRemove(connectionId, out _);
            entry.LastSeenAt = DateTime.UtcNow;
            nowEmpty = entry.ConnectionIds.IsEmpty;
        }

        // Keep the entry around so we can return LastSeenAt; do not remove it.
        return Task.FromResult(nowEmpty);
    }

    public Task<bool> IsOnlineAsync(Guid userId)
    {
        return Task.FromResult(
            _users.TryGetValue(userId, out var entry) && !entry.ConnectionIds.IsEmpty);
    }

    public Task<UserPresenceDto[]> GetPresenceAsync(IEnumerable<Guid> userIds)
    {
        var result = userIds
            .Distinct()
            .Select(id =>
            {
                if (_users.TryGetValue(id, out var entry))
                {
                    var online = !entry.ConnectionIds.IsEmpty;
                    return new UserPresenceDto(id, online, online ? null : entry.LastSeenAt);
                }
                return new UserPresenceDto(id, false, null);
            })
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<Guid[]> GetOnlineUsersAsync()
    {
        var ids = _users
            .Where(kvp => !kvp.Value.ConnectionIds.IsEmpty)
            .Select(kvp => kvp.Key)
            .ToArray();
        return Task.FromResult(ids);
    }
}
