namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Abstraction over file storage (local filesystem or S3-compatible bucket).
/// Configured via "Storage:Type" in appsettings — "local" (default) or "s3".
/// </summary>
public interface IMediaStorageService
{
    /// <summary>
    /// Persists a file stream and returns its public URL.
    /// </summary>
    /// <param name="content">Readable file stream. The caller is responsible for disposal.</param>
    /// <param name="fileName">Original file name (used to determine extension).</param>
    /// <param name="folder">Sub-folder / prefix (e.g. "avatars", "covers").</param>
    Task<string> StoreAsync(Stream content, string fileName, string folder);

    /// <summary>
    /// Deletes a previously stored file by its public URL.
    /// No-ops silently if the file does not exist.
    /// </summary>
    Task DeleteAsync(string url);
}
