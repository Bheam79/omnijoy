using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Resizes and converts an uploaded image to WebP at the dimensions
/// appropriate for the given storage folder.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Processes <paramref name="input"/> and returns a new
    /// <see cref="System.IO.MemoryStream"/> (positioned at the origin) containing a
    /// WebP-encoded image at the correct dimensions for <paramref name="folder"/>.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> ProcessImageAsync(Stream input, ImageFolder folder);
}
