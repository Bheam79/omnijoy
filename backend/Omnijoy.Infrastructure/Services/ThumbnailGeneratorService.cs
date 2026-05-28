using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Long-running hosted service that dequeues thumbnail generation jobs from
/// <see cref="IBackgroundTaskQueue"/> and executes them one at a time.
/// </summary>
public sealed class ThumbnailGeneratorService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<ThumbnailGeneratorService> _logger;

    public ThumbnailGeneratorService(
        IBackgroundTaskQueue queue,
        ILogger<ThumbnailGeneratorService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThumbnailGeneratorService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<CancellationToken, ValueTask> workItem;
            try
            {
                workItem = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await workItem(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing a thumbnail job.");
            }
        }

        _logger.LogInformation("ThumbnailGeneratorService stopped.");
    }
}
