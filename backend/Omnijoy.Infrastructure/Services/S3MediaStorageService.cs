using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Stores files in an S3-compatible object store (AWS S3, MinIO, Backblaze B2, etc.).
///
/// Required configuration keys (appsettings.json or environment variables):
///   Storage:S3:ServiceUrl     — endpoint URL (e.g. "https://s3.amazonaws.com")
///   Storage:S3:BucketName     — bucket name
///   Storage:S3:AccessKey      — access key ID
///   Storage:S3:SecretKey      — secret access key
///   Storage:S3:PublicBaseUrl  — optional public base URL for returned file URLs.
///                               Defaults to "{ServiceUrl}/{BucketName}".
/// </summary>
public class S3MediaStorageService : IMediaStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public S3MediaStorageService(IConfiguration config)
    {
        var serviceUrl = config["Storage:S3:ServiceUrl"]
            ?? throw new InvalidOperationException("Storage:S3:ServiceUrl is not configured.");
        _bucketName = config["Storage:S3:BucketName"]
            ?? throw new InvalidOperationException("Storage:S3:BucketName is not configured.");
        var accessKey = config["Storage:S3:AccessKey"]
            ?? throw new InvalidOperationException("Storage:S3:AccessKey is not configured.");
        var secretKey = config["Storage:S3:SecretKey"]
            ?? throw new InvalidOperationException("Storage:S3:SecretKey is not configured.");

        _publicBaseUrl = config["Storage:S3:PublicBaseUrl"]
            ?? $"{serviceUrl.TrimEnd('/')}/{_bucketName}";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true, // Required for MinIO and most S3-compatible services

            // AWSSDK.S3 3.7.412+ defaults to sending an extra CRC32 checksum header
            // (x-amz-sdk-checksum-algorithm + x-amz-checksum-crc32) on every PutObject.
            // MinIO and other S3-compatible stores reject these unknown headers with
            // "Access Denied", breaking uploads. Restrict checksum calculation /
            // validation to operations that strictly require it.
            // See https://github.com/minio/minio/issues/20845
            RequestChecksumCalculation  = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation  = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        _s3 = new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public async Task<string> StoreAsync(Stream content, string fileName, string folder)
    {
        if (content.Length == 0)
            throw new ArgumentException("Uploaded file is empty.");

        if (content.Length > MaxFileSizeBytes)
            throw new ArgumentException($"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed.");

        var key = $"{folder}/{Guid.NewGuid()}{ext}";
        var contentType = GetContentType(ext);

        var transferUtility = new TransferUtility(_s3);
        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = content,
            Key = key,
            BucketName = _bucketName,
            ContentType = contentType,
            // Do NOT set CannedACL — MinIO rejects per-object ACL operations.
            // Public read access is handled by the bucket-level anonymous download policy.

            // Belt-and-suspenders against the AWSSDK.S3 3.7.412+ default integrity
            // check that breaks MinIO uploads with "Access Denied". The
            // RequestChecksumCalculation = WHEN_REQUIRED on AmazonS3Config above
            // is the primary guard; setting this on the request itself ensures
            // that even if upstream re-enables a default, single-part uploads
            // stay free of the unsupported x-amz-checksum-* headers.
            DisableDefaultChecksumValidation = true,
        };

        await transferUtility.UploadAsync(uploadRequest);

        return $"{_publicBaseUrl.TrimEnd('/')}/{key}";
    }

    public async Task DeleteAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var basePrefix = _publicBaseUrl.TrimEnd('/') + "/";
        if (!url.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
            return;

        var key = url[basePrefix.Length..];
        if (string.IsNullOrEmpty(key))
            return;

        try
        {
            await _s3.DeleteObjectAsync(_bucketName, key);
        }
        catch (AmazonS3Exception)
        {
            // Best-effort deletion — silently ignore missing files
        }
    }

    private static string GetContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        _                 => "application/octet-stream",
    };
}
