using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="LocalMediaStorageService"/> using a temporary directory
/// as the web root. No HTTPS or ASP.NET Core runtime is required.
/// </summary>
public class LocalMediaStorageServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalMediaStorageService _sut;

    public LocalMediaStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"omni_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns(_tempRoot);

        _sut = new LocalMediaStorageService(envMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── StoreAsync — validation ───────────────────────────────────────────────

    [Fact]
    public async Task Store_EmptyStream_ThrowsArgumentException()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());

        await _sut.Invoking(s => s.StoreAsync(stream, "photo.jpg", "avatars"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task Store_InvalidExtension_ThrowsArgumentException()
    {
        using var stream = new MemoryStream(new byte[10]);

        await _sut.Invoking(s => s.StoreAsync(stream, "file.exe", "avatars"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task Store_ImageExceedsMaxSize_ThrowsArgumentException()
    {
        // 6 MB image (max is 5 MB)
        var oversized = new byte[6 * 1024 * 1024 + 1];
        using var stream = new MemoryStream(oversized);

        await _sut.Invoking(s => s.StoreAsync(stream, "big.jpg", "avatars"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maximum*");
    }

    [Fact]
    public async Task Store_VideoExceedsMaxSize_ThrowsArgumentException()
    {
        // 201 MB video (max is 200 MB)
        var oversized = new byte[201L * 1024 * 1024 + 1];
        using var stream = new MemoryStream(oversized);

        await _sut.Invoking(s => s.StoreAsync(stream, "clip.mp4", "posts/video"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maximum*");
    }

    [Fact]
    public async Task Store_DocumentExceedsMaxSize_ThrowsArgumentException()
    {
        // 51 MB doc (max is 50 MB)
        var oversized = new byte[51L * 1024 * 1024 + 1];
        using var stream = new MemoryStream(oversized);

        await _sut.Invoking(s => s.StoreAsync(stream, "report.pdf", "messages/file"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maximum*");
    }

    // ── StoreAsync — happy paths ──────────────────────────────────────────────

    [Fact]
    public async Task Store_ValidJpeg_SavesFileAndReturnsRelativeUrl()
    {
        using var stream = new MemoryStream(new byte[100]);

        var url = await _sut.StoreAsync(stream, "photo.jpg", "avatars");

        url.Should().StartWith("/uploads/avatars/");
        url.Should().EndWith(".jpg");

        var fullPath = Path.Combine(_tempRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public async Task Store_ValidPng_ReturnsPngUrl()
    {
        using var stream = new MemoryStream(new byte[200]);

        var url = await _sut.StoreAsync(stream, "image.png", "posts/images");

        url.Should().EndWith(".png");
    }

    [Fact]
    public async Task Store_ValidVideo_DetectedByFolder_StoresSuccessfully()
    {
        using var stream = new MemoryStream(new byte[500]);

        var url = await _sut.StoreAsync(stream, "clip.mp4", "posts/video");

        url.Should().StartWith("/uploads/posts/video/");
        url.Should().EndWith(".mp4");
    }

    [Fact]
    public async Task Store_ValidDocumentInFileFolder_StoresSuccessfully()
    {
        using var stream = new MemoryStream(new byte[1000]);

        var url = await _sut.StoreAsync(stream, "report.pdf", "messages/file");

        url.Should().StartWith("/uploads/messages/file/");
        url.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task Store_CreatesDirectoryIfNotExists()
    {
        var folder = "brand/new/folder";
        using var stream = new MemoryStream(new byte[50]);

        var url = await _sut.StoreAsync(stream, "icon.jpg", folder);

        url.Should().StartWith($"/uploads/{folder}/");
        var dir = Path.Combine(_tempRoot, "uploads", folder);
        Directory.Exists(dir).Should().BeTrue();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_NullUrl_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync(null!);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Delete_EmptyUrl_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync(string.Empty);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Delete_UrlNotUnderUploads_DoesNotThrow()
    {
        // External URL — must not attempt to delete
        var act = () => _sut.DeleteAsync("https://cdn.example.com/photo.jpg");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Delete_RelativeUrlNotUnderUploads_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync("/media/photo.jpg");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Delete_ValidUrl_DeletesFile()
    {
        // First store a file, then delete it.
        using var stream = new MemoryStream(new byte[50]);
        var url = await _sut.StoreAsync(stream, "toDelete.jpg", "avatars");

        await _sut.DeleteAsync(url);

        var fullPath = Path.Combine(_tempRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonExistentFile_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync("/uploads/avatars/ghost.jpg");

        await act.Should().NotThrowAsync();
    }
}
