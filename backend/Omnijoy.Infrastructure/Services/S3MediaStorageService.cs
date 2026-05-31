using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly string _serviceUrl;
    private readonly string _accessKey;
    private readonly ILogger<S3MediaStorageService> _logger;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public S3MediaStorageService(IConfiguration config, ILogger<S3MediaStorageService> logger)
    {
        _logger = logger;
        _serviceUrl = config["Storage:S3:ServiceUrl"]
            ?? throw new InvalidOperationException("Storage:S3:ServiceUrl is not configured.");
        _bucketName = config["Storage:S3:BucketName"]
            ?? throw new InvalidOperationException("Storage:S3:BucketName is not configured.");
        _accessKey = config["Storage:S3:AccessKey"]
            ?? throw new InvalidOperationException("Storage:S3:AccessKey is not configured.");
        var secretKey = config["Storage:S3:SecretKey"]
            ?? throw new InvalidOperationException("Storage:S3:SecretKey is not configured.");

        _publicBaseUrl = config["Storage:S3:PublicBaseUrl"]
            ?? $"{_serviceUrl.TrimEnd('/')}/{_bucketName}";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = _serviceUrl,
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

        _s3 = new AmazonS3Client(_accessKey, secretKey, s3Config);
    }

    /// <summary>
    /// Test-only constructor that accepts a pre-built <see cref="IAmazonS3"/> client,
    /// bypassing configuration-based setup.
    /// </summary>
    internal S3MediaStorageService(
        IAmazonS3 s3,
        string bucketName,
        string publicBaseUrl,
        ILogger<S3MediaStorageService> logger,
        string serviceUrl = "",
        string accessKey = "")
    {
        _s3             = s3;
        _bucketName     = bucketName;
        _publicBaseUrl  = publicBaseUrl;
        _logger         = logger;
        _serviceUrl     = serviceUrl;
        _accessKey      = accessKey;
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

        // Use PutObjectAsync directly rather than TransferUtility so that
        //   a) the method is unit-testable via IAmazonS3 mocks, and
        //   b) we avoid the TransferUtility telemetry bootstrap that crashes
        //      against the older OTEL shim that ships in some test environments.
        // All uploaded files are < 5 MB (enforced above), so single-part upload
        // is always the right strategy.  TransferUtility adds multi-part
        // splitting only beyond 16 MB anyway.
        var putRequest = new PutObjectRequest
        {
            InputStream  = content,
            Key          = key,
            BucketName   = _bucketName,
            ContentType  = contentType,
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

        try
        {
            await _s3.PutObjectAsync(putRequest);
        }
        catch (AmazonS3Exception ex)
        {
            LogS3Failure("S3 upload failed", ex, key);
            throw;
        }

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

    /// <summary>
    /// Verifies that the configured bucket exists and the credentials are
    /// accepted by the endpoint. Called once at startup by
    /// <see cref="S3StorageStartupProbe"/>.
    ///
    /// <para>
    /// Failures are logged with the full MinIO / S3 error response body
    /// (via <see cref="S3DiagnosticsLogging.BuildErrorContext"/>) but never
    /// rethrown — a credential or DNS bug at boot time should surface as a
    /// loud log line, not crash the API and take the rest of the platform
    /// down with it. The first user upload will produce the same enriched
    /// log line if the underlying problem hasn't been fixed.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> if the bucket was reachable; <c>false</c> if any
    ///     exception (S3 or otherwise) was caught and logged.</returns>
    public async Task<bool> ProbeBucketAsync(CancellationToken ct = default)
    {
        _logger.LogInformation(
            "S3 storage probe starting. Endpoint={Endpoint} Bucket={Bucket} AccessKeyPrefix={AccessKeyPrefix}",
            _serviceUrl,
            _bucketName,
            S3DiagnosticsLogging.FormatAccessKeyPrefix(_accessKey));

        try
        {
            await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                // Use a key that doesn't need to exist — a 404 means "creds work, bucket exists".
                Key = "__omnijoy_probe__",
            }, ct);

            _logger.LogInformation(
                "S3 storage probe succeeded. Endpoint={Endpoint} Bucket={Bucket}",
                _serviceUrl, _bucketName);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                           && string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            // 404 NoSuchKey is the *success* case for our probe — credentials
            // were accepted and the bucket exists; we just asked for a key
            // that doesn't exist. NoSuchBucket falls through to the catch below.
            _logger.LogInformation(
                "S3 storage probe succeeded (404 for probe key). Endpoint={Endpoint} Bucket={Bucket}",
                _serviceUrl, _bucketName);
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            LogS3Failure("S3 storage probe failed", ex, objectKey: null, logLevel: LogLevel.Error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "S3 storage probe failed with non-S3 exception. Endpoint={Endpoint} Bucket={Bucket} AccessKeyPrefix={AccessKeyPrefix}",
                _serviceUrl, _bucketName, S3DiagnosticsLogging.FormatAccessKeyPrefix(_accessKey));
            return false;
        }
    }

    /// <summary>
    /// Emits the enriched S3-failure log line. Centralised so that every
    /// catch site (StoreAsync, ProbeBucketAsync, future DeleteAsync upgrade)
    /// produces the same structured payload and stays consistent.
    /// </summary>
    private void LogS3Failure(
        string message,
        AmazonS3Exception ex,
        string? objectKey,
        LogLevel logLevel = LogLevel.Error)
    {
        var ctx = S3DiagnosticsLogging.BuildErrorContext(
            ex, _serviceUrl, _bucketName, _accessKey, objectKey);

        _logger.Log(logLevel, ex,
            message + ". ErrorCode={ErrorCode} StatusCode={StatusCode} ErrorType={ErrorType} RequestId={RequestId} AmazonId2={AmazonId2} Endpoint={Endpoint} Bucket={Bucket} AccessKeyPrefix={AccessKeyPrefix} ObjectKey={ObjectKey} Message={Message} ResponseBody={ResponseBody}",
            ctx["ErrorCode"],
            ctx["StatusCode"],
            ctx["ErrorType"],
            ctx["RequestId"],
            ctx["AmazonId2"],
            ctx["Endpoint"],
            ctx["Bucket"],
            ctx["AccessKeyPrefix"],
            ctx["ObjectKey"],
            ctx["Message"],
            ctx["ResponseBody"]);
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
