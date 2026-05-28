namespace Omnijoy.Core.Models.Enums;

/// <summary>
/// Destination folder that drives resize / crop decisions in
/// <see cref="Omnijoy.Core.Interfaces.IImageProcessingService"/>.
/// </summary>
public enum ImageFolder
{
    /// <summary>256 × 256, crop-to-fill (profile photos, company logos)</summary>
    Avatar,

    /// <summary>1200 × 630, fit-pad (profile, event and company banner images)</summary>
    Cover,

    /// <summary>≤ 1920 px wide, aspect ratio preserved (post photo attachments)</summary>
    PostImage,

    /// <summary>480 × 270, fit (video poster frames)</summary>
    Thumbnail,
}
