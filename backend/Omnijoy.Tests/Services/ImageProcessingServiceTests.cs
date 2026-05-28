using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Verifies that <see cref="ImageProcessingService"/> produces a WebP-encoded
/// image at the correct dimensions for every <see cref="ImageFolder"/> value.
/// </summary>
public class ImageProcessingServiceTests
{
    private readonly ImageProcessingService _sut = new();

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an in-memory PNG image with the given dimensions and returns it
    /// as a seekable <see cref="MemoryStream"/>.
    /// </summary>
    private static async Task<MemoryStream> MakePngStreamAsync(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>
    /// Loads an image from a stream and returns it together with the detected format.
    /// </summary>
    private static async Task<(Image<Rgba32> image, IImageFormat format)> LoadResultAsync(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var format = await Image.DetectFormatAsync(stream);
        stream.Seek(0, SeekOrigin.Begin);
        var image = await Image.LoadAsync<Rgba32>(stream);
        return (image, format!);
    }

    // ── output format ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ImageFolder.Avatar)]
    [InlineData(ImageFolder.Cover)]
    [InlineData(ImageFolder.PostImage)]
    [InlineData(ImageFolder.Thumbnail)]
    public async Task ProcessImageAsync_AlwaysOutputsWebP(ImageFolder folder)
    {
        await using var input = await MakePngStreamAsync(800, 600);

        await using var result = await _sut.ProcessImageAsync(input, folder);

        var (img, fmt) = await LoadResultAsync(result);
        img.Dispose();
        fmt.DefaultMimeType.Should().Be("image/webp");
    }

    // ── Avatar: 256 × 256 crop ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessImageAsync_Avatar_Landscape_Produces256x256()
    {
        await using var input = await MakePngStreamAsync(1200, 800);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Avatar);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(256);
            img.Height.Should().Be(256);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_Avatar_Portrait_Produces256x256()
    {
        await using var input = await MakePngStreamAsync(400, 800);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Avatar);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(256);
            img.Height.Should().Be(256);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_Avatar_SmallImage_StillProduces256x256()
    {
        await using var input = await MakePngStreamAsync(100, 100);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Avatar);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(256);
            img.Height.Should().Be(256);
        }
    }

    // ── Cover: 1200 × 630 pad ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessImageAsync_Cover_AlwaysProduces1200x630()
    {
        await using var input = await MakePngStreamAsync(1920, 1080);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Cover);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(1200);
            img.Height.Should().Be(630);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_Cover_SmallImage_StillProduces1200x630()
    {
        await using var input = await MakePngStreamAsync(300, 200);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Cover);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(1200);
            img.Height.Should().Be(630);
        }
    }

    // ── PostImage: ≤ 1920 px wide, aspect preserved ───────────────────────────

    [Fact]
    public async Task ProcessImageAsync_PostImage_WiderThan1920_DownscalesToMaxWidth()
    {
        // 3840 × 2160 (4K) → should become 1920 × 1080
        await using var input = await MakePngStreamAsync(3840, 2160);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.PostImage);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(1920);
            img.Height.Should().Be(1080);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_PostImage_ExactlyAt1920_Unchanged()
    {
        await using var input = await MakePngStreamAsync(1920, 1080);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.PostImage);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(1920);
            img.Height.Should().Be(1080);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_PostImage_NarrowerThan1920_NotUpscaled()
    {
        await using var input = await MakePngStreamAsync(800, 600);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.PostImage);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(800);
            img.Height.Should().Be(600);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_PostImage_AspectRatioPreserved()
    {
        // 2560 × 1440 (16:9) → 1920 × 1080 (16:9 preserved)
        await using var input = await MakePngStreamAsync(2560, 1440);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.PostImage);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(1920);
            // height = round(1440 * 1920 / 2560) = round(1080) = 1080
            img.Height.Should().Be(1080);
        }
    }

    // ── Thumbnail: 480 × 270 fit (scale to fit, no pad) ─────────────────────

    [Fact]
    public async Task ProcessImageAsync_Thumbnail_16x9_FitsExactly480x270()
    {
        await using var input = await MakePngStreamAsync(1920, 1080);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Thumbnail);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().Be(480);
            img.Height.Should().Be(270);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_Thumbnail_NeverExceeds480Width()
    {
        await using var input = await MakePngStreamAsync(1920, 1440); // 4:3

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Thumbnail);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().BeLessThanOrEqualTo(480);
            img.Height.Should().BeLessThanOrEqualTo(270);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_Thumbnail_SmallImage_NotUpscaled()
    {
        await using var input = await MakePngStreamAsync(100, 56); // smaller than target

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Thumbnail);

        var (img, _) = await LoadResultAsync(result);
        using (img)
        {
            img.Width.Should().BeLessThanOrEqualTo(480);
            img.Height.Should().BeLessThanOrEqualTo(270);
        }
    }

    // ── Output stream is readable and seekable ────────────────────────────────

    [Fact]
    public async Task ProcessImageAsync_ReturnsStreamPositionedAtBeginning()
    {
        await using var input = await MakePngStreamAsync(400, 300);

        await using var result = await _sut.ProcessImageAsync(input, ImageFolder.Avatar);

        result.Position.Should().Be(0);
        result.CanRead.Should().BeTrue();
    }
}
