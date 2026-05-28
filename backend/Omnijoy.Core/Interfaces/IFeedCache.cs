using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Distributed-cache abstraction for feed reads.
///
/// Two cache families:
///   • Per-user feed page 1 (key: <c>feed:{userId}:p1</c>, TTL 60s).
///   • Trending posts list  (key: <c>trending:posts</c>, refreshed every 5 min).
///
/// Both are JSON-serialized into <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>,
/// so any IDistributedCache implementation (Redis in prod, in-memory in dev/test)
/// works transparently.
///
/// Trade-offs:
///   • Page 1 entries can be up to 60 seconds stale for users whose followed
///     authors didn't push an invalidation (we only invalidate on a NewPost
///     SignalR event). Edits / deletes / reactions to existing posts are NOT
///     invalidated; they surface on the next TTL expiry.
///   • Pages 2+ are never cached — they're rarer (scroll past the top) and
///     pagination invalidation gets hairy.
///   • Trending list is best-effort; it lags by the refresh interval (5 min).
/// </summary>
public interface IFeedCache
{
    // ── Per-user feed page 1 ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the cached page-1 feed for <paramref name="userId"/>, or null
    /// on miss / deserialization failure.
    /// </summary>
    Task<FeedPageResult?> GetUserFeedPage1Async(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Stores the given feed page (page 1 only) with a 60-second TTL.
    /// </summary>
    Task SetUserFeedPage1Async(Guid userId, FeedPageResult result, CancellationToken ct = default);

    /// <summary>
    /// Drops the cached page-1 entry for <paramref name="userId"/>.
    /// Called when a followed author publishes a new post so the next read
    /// rebuilds the feed from the database.
    /// </summary>
    Task InvalidateUserFeedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Convenience: invalidate many users in one call.</summary>
    Task InvalidateUserFeedsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    // ── Trending posts ────────────────────────────────────────────────────────

    /// <summary>Cached trending-posts list (null on cache miss).</summary>
    Task<IReadOnlyList<PostDto>?> GetTrendingPostsAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the trending-posts list. TTL is generous (10 minutes); the
    /// background refresher rewrites it every 5 minutes.
    /// </summary>
    Task SetTrendingPostsAsync(IReadOnlyList<PostDto> posts, CancellationToken ct = default);
}
