using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// <see cref="IFeedCache"/> implementation backed by <see cref="IDistributedCache"/>.
///
/// In production this is Redis (registered via AddStackExchangeRedisCache);
/// in dev/test it transparently falls back to AddDistributedMemoryCache.
///
/// Failures (Redis down, JSON corruption) are logged and degraded into a
/// cache-miss — feed reads NEVER fail because of caching.
/// </summary>
public class DistributedFeedCache : IFeedCache
{
    public const int FeedTtlSeconds = 60;
    public const int TrendingTtlSeconds = 600; // 10-minute safety net; refresher rewrites every 5 min

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedFeedCache> _log;

    public DistributedFeedCache(IDistributedCache cache, ILogger<DistributedFeedCache> log)
    {
        _cache = cache;
        _log = log;
    }

    // ── Key helpers ───────────────────────────────────────────────────────────

    internal static string UserFeedKey(Guid userId) => $"feed:{userId:N}:p1";
    internal const string TrendingKey = "trending:posts";

    // ── Per-user feed page 1 ──────────────────────────────────────────────────

    public async Task<PagedResult<FeedItemDto>?> GetUserFeedPage1Async(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(UserFeedKey(userId), ct);
            if (bytes is null || bytes.Length == 0) return null;
            return JsonSerializer.Deserialize<PagedResult<FeedItemDto>>(bytes, JsonOptions);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeedCache: failed reading page-1 for user {UserId}", userId);
            return null;
        }
    }

    public async Task SetUserFeedPage1Async(Guid userId, PagedResult<FeedItemDto> result, CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(FeedTtlSeconds),
            };
            await _cache.SetAsync(UserFeedKey(userId), bytes, options, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeedCache: failed writing page-1 for user {UserId}", userId);
        }
    }

    public async Task InvalidateUserFeedAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(UserFeedKey(userId), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeedCache: failed invalidating page-1 for user {UserId}", userId);
        }
    }

    public async Task InvalidateUserFeedsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        foreach (var id in userIds)
        {
            await InvalidateUserFeedAsync(id, ct);
        }
    }

    // ── Trending posts ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PostDto>?> GetTrendingPostsAsync(CancellationToken ct = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(TrendingKey, ct);
            if (bytes is null || bytes.Length == 0) return null;
            return JsonSerializer.Deserialize<PostDto[]>(bytes, JsonOptions);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeedCache: failed reading trending list");
            return null;
        }
    }

    public async Task SetTrendingPostsAsync(IReadOnlyList<PostDto> posts, CancellationToken ct = default)
    {
        try
        {
            var arr = posts as PostDto[] ?? posts.ToArray();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(arr, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(TrendingTtlSeconds),
            };
            await _cache.SetAsync(TrendingKey, bytes, options, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeedCache: failed writing trending list");
        }
    }
}
