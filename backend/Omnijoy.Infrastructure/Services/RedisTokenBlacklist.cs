using Microsoft.Extensions.Caching.Distributed;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Stores revoked JWT JTIs in IDistributedCache (Redis in production,
/// MemoryDistributedCache in single-node dev / tests).
///
/// Key pattern: <c>bl:{jti}</c> with TTL = remaining token lifetime.
/// Stale entries are evicted automatically when the token would have expired anyway.
/// </summary>
public class RedisTokenBlacklist : ITokenBlacklist
{
    private readonly IDistributedCache _cache;

    public RedisTokenBlacklist(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public Task BlacklistAsync(string jti, TimeSpan ttl)
    {
        return _cache.SetStringAsync(
            $"bl:{jti}",
            "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1)
            });
    }

    /// <inheritdoc/>
    public async Task<bool> IsBlacklistedAsync(string jti)
    {
        var value = await _cache.GetStringAsync($"bl:{jti}");
        return value is not null;
    }
}
