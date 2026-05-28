namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Enqueues an async thumbnail-generation job for a video post attachment.
/// The actual work runs in a background service (<c>ThumbnailGeneratorService</c>)
/// which invokes FFmpeg to extract a frame and stores the result via
/// <see cref="IMediaStorageService"/>.
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Enqueues a job that extracts a frame from the video at <paramref name="videoUrl"/>,
    /// resizes it to 480×270 WebP, stores it under <c>thumbnails/</c>, and updates
    /// <c>PostMedia.ThumbnailUrl</c> for the given <paramref name="postMediaId"/>.
    /// Returns as soon as the job is placed on the queue.
    /// </summary>
    Task EnqueueAsync(Guid postMediaId, string videoUrl);
}
