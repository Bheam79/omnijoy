using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Redis-backed presence tracker using IDistributedCache.
/// Suitable for multi-instance deployments — all nodes share presence state via Redis.
///
/// Key pattern: <c>presence:{userId}</c> → JSON-serialised <see cref="PresenceEntry"/>.
///
/// Limitation: <see cref="GetOnlineUsersAsync"/> returns an empty array because
/// IDistributedCache does not expose key enumeration. Use a dedicated Redis set
/// (via IConnectionMultiplexer) if you need full online-user enumeration at scale.
/// </summary>
public class RedisPresenceTracker : IPresenceTracker
{
    private readonly IDistributedCache _cache;

    // Keep presence entries alive for a week; they are refreshed on every
    // connect/disconnect so the TTL effectively acts as an eviction guard.
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisPresenceTracker(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectedAsync(Guid userId, string connectionId)
    {
        var entry = await GetOrCreateAsync(userId);
        var wasEmpty = entry.ConnectionIds.Count == 0;
        entry.ConnectionIds.Add(connectionId);
        entry.LastSeenAt = DateTime.UtcNow;
        await SaveAsync(userId, entry);
        return wasEmpty; // true = first connection → just came online
    }

    /// <inheritdoc/>
    public async Task<bool> DisconnectedAsync(Guid userId, string connectionId)
    {
        var entry = await GetOrCreateAsync(userId);
        entry.ConnectionIds.Remove(connectionId);
        entry.LastSeenAt = DateTime.UtcNow;
        await SaveAsync(userId, entry);
        return entry.ConnectionIds.Count == 0; // true = last connection dropped → went offline
    }

    /// <inheritdoc/>
    public async Task<bool> IsOnlineAsync(Guid userId)
    {
        var entry = await GetOrCreateAsync(userId);
        return entry.ConnectionIds.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<UserPresenceDto[]> GetPresenceAsync(IEnumerable<Guid> userIds)
    {
        var result = new List<UserPresenceDto>();
        foreach (var id in userIds.Distinct())
        {
            var entry = await GetOrCreateAsync(id);
            var online = entry.ConnectionIds.Count > 0;
            result.Add(new UserPresenceDto(id, online, online ? null : entry.LastSeenAt));
        }
        return [.. result];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Not supported by IDistributedCache — returns an empty array.
    /// Use the in-memory tracker or a direct Redis SMEMBERS call if you need this.
    /// </remarks>
    public Task<Guid[]> GetOnlineUsersAsync() => Task.FromResult(Array.Empty<Guid>());

    // ── Internal helpers ──────────────────────────────────────────────────────

    private sealed class PresenceEntry
    {
        public HashSet<string> ConnectionIds { get; set; } = [];
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }

    private async Task<PresenceEntry> GetOrCreateAsync(Guid userId)
    {
        var bytes = await _cache.GetAsync(PresenceKey(userId));
        if (bytes is null or { Length: 0 })
            return new PresenceEntry();

        return JsonSerializer.Deserialize<PresenceEntry>(bytes, _jsonOpts) ?? new PresenceEntry();
    }

    private Task SaveAsync(Guid userId, PresenceEntry entry)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry);
        return _cache.SetAsync(PresenceKey(userId), bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PresenceTtl
        });
    }

    private static string PresenceKey(Guid userId) => $"presence:{userId}";
}
