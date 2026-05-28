using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Stores revoked JWT JTIs in IDistributedCache (Redis in production,
/// MemoryDistributedCache in single-node dev / tests).
///
/// Key pattern: <c>bl:{jti}</c> with TTL = remaining token lifetime.
/// Stale entries are evicted automatically when the token would have expired anyway.
///
/// Resilience: cache failures (Redis down, network blip, etc.) are logged and
/// degraded gracefully. <see cref="IsBlacklistedAsync"/> returns <c>false</c>
/// (assume not blacklisted) so the JWT pipeline keeps working when Redis is
/// unavailable — a brief auth-bypass window for revoked tokens is preferable to
/// 500ing every authenticated request. <see cref="BlacklistAsync"/> swallows
/// errors so logout doesn't crash the request (the token is still time-bounded
/// by its own expiry).
/// </summary>
public class RedisTokenBlacklist : ITokenBlacklist
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisTokenBlacklist> _log;

    public RedisTokenBlacklist(IDistributedCache cache, ILogger<RedisTokenBlacklist> log)
    {
        _cache = cache;
        _log = log;
    }

    /// <inheritdoc/>
    public async Task BlacklistAsync(string jti, TimeSpan ttl)
    {
        try
        {
            await _cache.SetStringAsync(
                $"bl:{jti}",
                "1",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1)
                });
        }
        catch (Exception ex)
        {
            // Failing to record a revoked token is bad (it may still validate
            // until its natural expiry), but crashing logout is worse.
            _log.LogWarning(ex, "TokenBlacklist: failed to blacklist jti {Jti}", jti);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsBlacklistedAsync(string jti)
    {
        try
        {
            var value = await _cache.GetStringAsync($"bl:{jti}");
            return value is not null;
        }
        catch (Exception ex)
        {
            // If Redis is unreachable, assume the token is NOT blacklisted so
            // the JWT OnTokenValidated handler doesn't 500 every authenticated
            // request. Token still has to pass signature + expiry validation.
            _log.LogWarning(ex, "TokenBlacklist: failed to check jti {Jti}, assuming not blacklisted", jti);
            return false;
        }
    }
}
