using Microsoft.AspNetCore.Hosting;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Stores uploaded files on the local filesystem under
/// <c>{WebRootPath}/uploads/{folder}/{guid}{ext}</c> and serves them
/// as static files via the ASP.NET Core static-files middleware.
/// </summary>
public class LocalMediaStorageService : IMediaStorageService
{
    private readonly string _webRootPath;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public LocalMediaStorageService(IWebHostEnvironment env)
    {
        _webRootPath = env.WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> StoreAsync(Stream content, string fileName, string folder)
    {
        if (content.Length == 0)
            throw new ArgumentException("Uploaded file is empty.");

        if (content.Length > MaxFileSizeBytes)
            throw new ArgumentException($"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Permitted: {string.Join(", ", AllowedExtensions)}");

        var uploadDir = Path.Combine(_webRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadDir);

        var newFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, newFileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await content.CopyToAsync(fileStream);

        // Return a root-relative URL served by static files middleware
        return $"/uploads/{folder}/{newFileName}";
    }

    public Task DeleteAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.CompletedTask;

        // Only delete files we own (relative URLs starting with /uploads/)
        if (!url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_webRootPath, relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }
}
