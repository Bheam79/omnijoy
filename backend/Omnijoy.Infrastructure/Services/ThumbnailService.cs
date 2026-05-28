using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Enqueues asynchronous thumbnail-generation jobs for uploaded video posts.
///
/// <para>
/// <see cref="EnqueueAsync"/> places a work item on the <see cref="IBackgroundTaskQueue"/>
/// and returns immediately.  The work item is later dequeued and executed by
/// <see cref="ThumbnailGeneratorService"/>.
/// </para>
///
/// <para>
/// The job logic lives in <see cref="RunJobAsync"/> (internal for testability):
/// <list type="number">
///   <item>Copy the video from local disk (or download via HTTP) to a temp file.</item>
///   <item>Invoke <c>ffmpeg</c> via <see cref="IProcessRunner"/> to extract the frame at
///         t=1 s as a JPEG.</item>
///   <item>Resize the JPEG to 480×270 WebP via <see cref="IImageProcessingService"/>.</item>
///   <item>Persist the WebP under <c>thumbnails/</c> via <see cref="IMediaStorageService"/>.</item>
///   <item>Update <c>PostMedia.ThumbnailUrl</c> in the database.</item>
/// </list>
/// </para>
/// </summary>
public class ThumbnailService : IThumbnailService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProcessRunner _processRunner;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(
        IBackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        IProcessRunner processRunner,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ILogger<ThumbnailService> logger)
    {
        _queue             = queue;
        _scopeFactory      = scopeFactory;
        _processRunner     = processRunner;
        _env               = env;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(Guid postMediaId, string videoUrl)
    {
        await _queue.EnqueueAsync(async ct =>
        {
            await RunJobAsync(postMediaId, videoUrl, ct);
        });
    }

    // ── Job implementation ────────────────────────────────────────────────────

    /// <summary>
    /// Executes the thumbnail-generation pipeline for one video.
    /// Marked <c>internal</c> so unit tests can call it directly without going
    /// through the queue.
    /// </summary>
    internal async Task RunJobAsync(Guid postMediaId, string videoUrl, CancellationToken ct)
    {
        var tempVideoPath = Path.Combine(Path.GetTempPath(), $"omnijoy_vid_{Guid.NewGuid()}.tmp");
        var tempThumbPath = Path.Combine(Path.GetTempPath(), $"omnijoy_thumb_{Guid.NewGuid()}.jpg");

        try
        {
            _logger.LogInformation(
                "Generating thumbnail for PostMedia {PostMediaId} (url={VideoUrl}).",
                postMediaId, videoUrl);

            // 1 ── Obtain video bytes ──────────────────────────────────────────
            await FetchVideoAsync(videoUrl, tempVideoPath, ct);

            // 2 ── Extract frame at t=1s via FFmpeg ───────────────────────────
            var ffmpegArgs = $"-y -ss 1 -i \"{tempVideoPath}\" -vframes 1 -q:v 2 \"{tempThumbPath}\"";
            var exitCode = await _processRunner.RunAsync("ffmpeg", ffmpegArgs, ct);

            if (exitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg exited with code {exitCode} while generating thumbnail for PostMedia {postMediaId}.");

            // 3–4 ── Resize to 480×270 WebP + store ───────────────────────────
            await using var scope = _scopeFactory.CreateAsyncScope();
            var imageProcessor = scope.ServiceProvider.GetRequiredService<IImageProcessingService>();
            var storage        = scope.ServiceProvider.GetRequiredService<IMediaStorageService>();
            var db             = scope.ServiceProvider.GetRequiredService<OmnijoyDbContext>();

            string thumbnailUrl;
            await using (var jpegStream = File.OpenRead(tempThumbPath))
            await using (var webpStream = await imageProcessor.ProcessImageAsync(jpegStream, ImageFolder.Thumbnail))
            {
                thumbnailUrl = await storage.StoreAsync(webpStream, "thumbnail.webp", "thumbnails");
            }

            // 5 ── Persist thumbnail URL ───────────────────────────────────────
            var media = await db.PostMedia.FindAsync(new object[] { postMediaId }, ct);
            if (media is null)
            {
                _logger.LogWarning(
                    "PostMedia {PostMediaId} not found after thumbnail generation — skipping DB update.",
                    postMediaId);
                return;
            }

            media.ThumbnailUrl = thumbnailUrl;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Thumbnail stored at {ThumbnailUrl} for PostMedia {PostMediaId}.",
                thumbnailUrl, postMediaId);
        }
        finally
        {
            TryDelete(tempVideoPath);
            TryDelete(tempThumbPath);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Copies or downloads the video to <paramref name="destPath"/>.
    /// <list type="bullet">
    ///   <item>Relative URL (starts with <c>/uploads/</c>) → mapped to the local
    ///         wwwroot filesystem.</item>
    ///   <item>Absolute URL → downloaded via <see cref="IHttpClientFactory"/>.</item>
    /// </list>
    /// </summary>
    private async Task FetchVideoAsync(string videoUrl, string destPath, CancellationToken ct)
    {
        if (videoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var webRoot   = _env.WebRootPath ?? Directory.GetCurrentDirectory();
            var localPath = Path.Combine(
                webRoot,
                videoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            File.Copy(localPath, destPath, overwrite: true);
            return;
        }

        // Absolute URL (S3 / HTTP)
        using var client   = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var dest = File.OpenWrite(destPath);
        await response.Content.CopyToAsync(dest, ct);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
