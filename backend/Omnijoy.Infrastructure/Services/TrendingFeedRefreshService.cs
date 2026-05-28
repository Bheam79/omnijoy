using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Periodically recomputes the trending-posts list and caches it.
///
/// Trade-offs:
///   • Runs in every instance — multi-node deployments will recompute N times
///     per interval. The work is cheap (one ranked query, ~20 posts) and the
///     last writer wins; we accept the duplicate effort to avoid pulling in a
///     leader-election dependency.
///   • Refresh interval is 5 minutes. Cache TTL is 10 minutes so a single
///     missed refresh tick still serves data. A longer outage will eventually
///     surface a cache miss; the GET /api/feed/trending endpoint falls back
///     to a live DB query in that case.
/// </summary>
public class TrendingFeedRefreshService : BackgroundService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private const int TrendingTake = 20;

    private readonly IServiceProvider _services;
    private readonly ILogger<TrendingFeedRefreshService> _log;

    public TrendingFeedRefreshService(
        IServiceProvider services,
        ILogger<TrendingFeedRefreshService> log)
    {
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay: let the app finish starting before hitting the DB.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshOnceAsync(stoppingToken);

            try { await Task.Delay(RefreshInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// Single refresh tick. Exposed (internal) so tests can drive one cycle
    /// without spinning the long-running loop.
    /// </summary>
    internal async Task RefreshOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var posts = scope.ServiceProvider.GetRequiredService<IPostService>();
            var cache = scope.ServiceProvider.GetRequiredService<IFeedCache>();

            var trending = await posts.GetTrendingPostsAsync(TrendingTake, ct);
            await cache.SetTrendingPostsAsync(trending, ct);

            _log.LogDebug("TrendingFeedRefresh: cached {Count} posts", trending.Count);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "TrendingFeedRefresh: refresh tick failed");
        }
    }
}
