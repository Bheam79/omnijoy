using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Synchronous resize + WebP conversion at upload time.
/// Uses SixLabors.ImageSharp (pure managed code, no native dependencies).
/// </summary>
public sealed class ImageProcessingService : IImageProcessingService
{
    // quality 82 ≈ good visual fidelity / small file size trade-off
    private static readonly WebpEncoder Encoder = new() { Quality = 82 };

    /// <inheritdoc />
    public async Task<Stream> ProcessImageAsync(Stream input, ImageFolder folder)
    {
        using var image = await Image.LoadAsync(input);

        switch (folder)
        {
            case ImageFolder.Avatar:
                // 256 × 256 — crop to fill, centred
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size     = new Size(256, 256),
                    Mode     = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                }));
                break;

            case ImageFolder.Cover:
                // 1200 × 630 — scale to fit, letterbox/pillarbox with black bars
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size     = new Size(1200, 630),
                    Mode     = ResizeMode.Pad,
                    PadColor = Color.Black,
                }));
                break;

            case ImageFolder.PostImage:
                // ≤ 1920 px wide — downscale only, aspect ratio preserved
                if (image.Width > 1920)
                {
                    var h = (int)Math.Round((double)image.Height * 1920 / image.Width);
                    image.Mutate(x => x.Resize(1920, h));
                }
                break;

            case ImageFolder.Thumbnail:
                // 480 × 270 — scale to fit (no pad, no crop)
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(480, 270),
                    Mode = ResizeMode.Max,
                }));
                break;
        }

        var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, Encoder);
        output.Seek(0, SeekOrigin.Begin);
        return output;
    }
}
