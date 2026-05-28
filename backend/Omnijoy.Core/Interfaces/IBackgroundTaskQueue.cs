namespace Omnijoy.Core.Interfaces;

/// <summary>
/// A channel-backed queue for fire-and-forget background work items.
/// Registered as a singleton; consumed by <c>ThumbnailGeneratorService</c>.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>Enqueues a work item to be executed by the background service.</summary>
    ValueTask EnqueueAsync(Func<CancellationToken, ValueTask> workItem, CancellationToken cancellationToken = default);

    /// <summary>Blocks (asynchronously) until a work item is available, then returns it.</summary>
    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}
