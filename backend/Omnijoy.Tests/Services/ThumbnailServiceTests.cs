using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ThumbnailService"/>.
/// The FFmpeg process runner and the storage service are both mocked so no real
/// FFmpeg binary or file-system access is required.
/// </summary>
public class ThumbnailServiceTests : IDisposable
{
    // ── shared mocks ─────────────────────────────────────────────────────────

    private readonly Mock<IBackgroundTaskQueue> _queueMock = new();
    private readonly Mock<IProcessRunner>       _runnerMock = new();
    private readonly Mock<IMediaStorageService> _storageMock = new();
    private readonly Mock<IImageProcessingService> _imageProcessorMock = new();
    private readonly Mock<IWebHostEnvironment>  _envMock = new();

    private readonly OmnijoyDbContext _db;
    private readonly ThumbnailService _sut;

    // Temp directory used by tests that create "video" files on disk.
    private readonly string _tempDir;

    public ThumbnailServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);

        _tempDir = Path.Combine(Path.GetTempPath(), $"omnijoy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _envMock.Setup(e => e.WebRootPath).Returns(_tempDir);

        // Image processor: return a tiny 1-byte stream as a stand-in WebP.
        _imageProcessorMock
            .Setup(p => p.ProcessImageAsync(It.IsAny<Stream>(), ImageFolder.Thumbnail))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x52, 0x49, 0x46, 0x46 }));  // "RIFF"

        // Storage: return a predictable URL.
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), "thumbnail.webp", "thumbnails"))
            .ReturnsAsync("/uploads/thumbnails/test-thumb.webp");

        // Build a scope factory mock that resolves the three scoped services.
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(IImageProcessingService))).Returns(_imageProcessorMock.Object);
        spMock.Setup(sp => sp.GetService(typeof(IMediaStorageService))).Returns(_storageMock.Object);
        spMock.Setup(sp => sp.GetService(typeof(OmnijoyDbContext))).Returns(_db);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        // HttpClientFactory is not needed for local-path tests; pass a no-op mock.
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        _sut = new ThumbnailService(
            _queueMock.Object,
            scopeFactoryMock.Object,
            _runnerMock.Object,
            _envMock.Object,
            httpClientFactoryMock.Object,
            NullLogger<ThumbnailService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── EnqueueAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnqueueAsync_CallsQueueOnce()
    {
        _queueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _sut.EnqueueAsync(Guid.NewGuid(), "/uploads/posts/videos/test.mp4");

        _queueMock.Verify(
            q => q.EnqueueAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── RunJobAsync: happy path ──────────────────────────────────────────────

    [Fact]
    public async Task RunJobAsync_LocalVideo_UpdatesThumbnailUrl()
    {
        // Arrange: seed a PostMedia row and put a dummy video file on disk.
        var postMedia = await SeedPostMediaAsync();
        var videoUrl  = CreateDummyVideoFile("uploads/posts/videos");

        SetupFfmpegMock();

        // Act
        await _sut.RunJobAsync(postMedia.Id, videoUrl, CancellationToken.None);

        // Assert: ThumbnailUrl written to the database.
        var updated = await _db.PostMedia.FindAsync(postMedia.Id);
        updated!.ThumbnailUrl.Should().Be("/uploads/thumbnails/test-thumb.webp");
    }

    [Fact]
    public async Task RunJobAsync_CallsFfmpegWithCorrectExecutable()
    {
        var postMedia = await SeedPostMediaAsync();
        var videoUrl  = CreateDummyVideoFile("uploads/posts/videos");

        SetupFfmpegMock();

        await _sut.RunJobAsync(postMedia.Id, videoUrl, CancellationToken.None);

        _runnerMock.Verify(
            r => r.RunAsync(
                "ffmpeg",
                It.Is<string>(a => a.Contains("-ss 1") && a.Contains("-vframes 1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunJobAsync_CallsImageProcessorWithThumbnailFolder()
    {
        var postMedia = await SeedPostMediaAsync();
        var videoUrl  = CreateDummyVideoFile("uploads/posts/videos");

        SetupFfmpegMock();

        await _sut.RunJobAsync(postMedia.Id, videoUrl, CancellationToken.None);

        _imageProcessorMock.Verify(
            p => p.ProcessImageAsync(It.IsAny<Stream>(), ImageFolder.Thumbnail),
            Times.Once);
    }

    [Fact]
    public async Task RunJobAsync_CallsStorageWithThumbnailsFolder()
    {
        var postMedia = await SeedPostMediaAsync();
        var videoUrl  = CreateDummyVideoFile("uploads/posts/videos");

        SetupFfmpegMock();

        await _sut.RunJobAsync(postMedia.Id, videoUrl, CancellationToken.None);

        _storageMock.Verify(
            s => s.StoreAsync(It.IsAny<Stream>(), "thumbnail.webp", "thumbnails"),
            Times.Once);
    }

    // ── RunJobAsync: FFmpeg failure ──────────────────────────────────────────

    [Fact]
    public async Task RunJobAsync_FfmpegNonZeroExit_ThrowsAndSkipsDbUpdate()
    {
        var postMedia = await SeedPostMediaAsync();
        var videoUrl  = CreateDummyVideoFile("uploads/posts/videos");

        // FFmpeg returns failure.
        _runnerMock
            .Setup(r => r.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act + Assert: should throw.
        await _sut.Invoking(s => s.RunJobAsync(postMedia.Id, videoUrl, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ffmpeg*");

        // DB row must not have been touched.
        var untouched = await _db.PostMedia.FindAsync(postMedia.Id);
        untouched!.ThumbnailUrl.Should().BeNull();
    }

    // ── RunJobAsync: PostMedia not found ─────────────────────────────────────

    [Fact]
    public async Task RunJobAsync_PostMediaNotFound_DoesNotThrow()
    {
        var missingId = Guid.NewGuid();
        var videoUrl  = CreateDummyVideoFile("uploads/posts/videos");

        SetupFfmpegMock();

        // Should complete without throwing even though the PostMedia row doesn't exist.
        await _sut.Invoking(s => s.RunJobAsync(missingId, videoUrl, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    // ── BackgroundTaskQueue ─────────────────────────────────────────────────

    [Fact]
    public async Task BackgroundTaskQueue_EnqueueDequeue_ReturnsSameItem()
    {
        var queue    = new BackgroundTaskQueue(capacity: 10);
        var executed = false;

        Func<CancellationToken, ValueTask> item = _ =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        };

        await queue.EnqueueAsync(item);
        var dequeued = await queue.DequeueAsync(CancellationToken.None);
        await dequeued(CancellationToken.None);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task BackgroundTaskQueue_Dequeue_CancelledToken_ThrowsOperationCanceled()
    {
        var queue = new BackgroundTaskQueue(capacity: 10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await queue.Invoking(q => q.DequeueAsync(cts.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BackgroundTaskQueue_Enqueue_NullWorkItem_ThrowsArgumentNullException()
    {
        var queue = new BackgroundTaskQueue(capacity: 10);

        await queue.Invoking(q => q.EnqueueAsync(null!).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
    }

    // ── ThumbnailGeneratorService ────────────────────────────────────────────

    [Fact]
    public async Task ThumbnailGeneratorService_ExecutesEnqueuedItem()
    {
        var queue   = new BackgroundTaskQueue(capacity: 10);
        var executed = false;

        await queue.EnqueueAsync(_ =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        });

        using var cts     = new CancellationTokenSource();
        var       service = new ThumbnailGeneratorService(queue, NullLogger<ThumbnailGeneratorService>.Instance);

        // Run the service briefly, then stop.
        var runTask = service.StartAsync(cts.Token);
        await Task.Delay(200); // enough time to process the item
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ThumbnailGeneratorService_ExceptionInJob_DoesNotCrashService()
    {
        var queue = new BackgroundTaskQueue(capacity: 10);

        await queue.EnqueueAsync(_ => throw new Exception("boom"));

        using var cts     = new CancellationTokenSource();
        var       service = new ThumbnailGeneratorService(queue, NullLogger<ThumbnailGeneratorService>.Instance);

        var runTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        await cts.CancelAsync();

        // StopAsync should complete without re-throwing the job exception.
        await service.Invoking(s => s.StopAsync(CancellationToken.None))
            .Should().NotThrowAsync();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a <see cref="PostMedia"/> row in the in-memory DB and returns it.
    /// </summary>
    private async Task<PostMedia> SeedPostMediaAsync()
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = "test@test.com",
            DisplayName = "Test",
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = user.Id,
            Author       = user,
            Content      = "Video post",
            PostType     = PostType.Video,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        var media = new PostMedia
        {
            Id        = Guid.NewGuid(),
            PostId    = post.Id,
            MediaType = MediaType.Video,
            Url       = "/uploads/posts/videos/test.mp4",
            Order     = 0,
        };

        _db.Users.Add(user);
        _db.Posts.Add(post);
        _db.PostMedia.Add(media);
        await _db.SaveChangesAsync();

        return media;
    }

    /// <summary>
    /// Creates a dummy (non-video) file on disk under <c>{_tempDir}/{relFolder}</c>
    /// and returns the root-relative URL.
    /// </summary>
    private string CreateDummyVideoFile(string relFolder)
    {
        var dir      = Path.Combine(_tempDir, relFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid()}.mp4";
        var path     = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01, 0x02 }); // placeholder bytes
        return $"/{relFolder.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}";
    }

    /// <summary>
    /// Sets up the <see cref="IProcessRunner"/> mock to create a minimal JPEG at
    /// the output path extracted from the ffmpeg argument string, and return exit code 0.
    /// </summary>
    private void SetupFfmpegMock()
    {
        _runnerMock
            .Setup(r => r.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, args, _) =>
            {
                // The ffmpeg args end with: -q:v 2 "<output-path>"
                // Extract the last double-quoted token.
                var lastClose  = args.LastIndexOf('"');
                var lastOpen   = args.LastIndexOf('"', lastClose - 1);
                var outputPath = args.Substring(lastOpen + 1, lastClose - lastOpen - 1);

                // Write a real 1×1 JPEG via ImageSharp so ImageProcessingService can load it.
                using var img = new Image<Rgba32>(1, 1);
                img.SaveAsJpeg(outputPath);

                return Task.FromResult(0);
            });
    }
}
