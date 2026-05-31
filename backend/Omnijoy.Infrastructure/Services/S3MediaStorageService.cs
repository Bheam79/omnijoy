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

        // Fail-fast on empty *or* whitespace credentials, not just null. The
        // env-var config provider returns the empty string when a referenced
        // variable is set to "" (or, with docker compose, when the bare
        // ${VAR} substitution finds the variable unset). A `?? throw` would
        // not trip on "", and the SDK would happily build a client with empty
        // credentials that sends anonymous requests — MinIO then replies
        // AccessDenied and the failure surfaces as an opaque 502 to clients.
        // See OMNIJOY-137 / OMNIJOY-138 for the production incident this
        // guards against.
        _serviceUrl = RequireNonEmpty(config, "Storage:S3:ServiceUrl");
        _bucketName = RequireNonEmpty(config, "Storage:S3:BucketName");
        _accessKey  = RequireNonEmpty(config, "Storage:S3:AccessKey");
        var secretKey = RequireNonEmpty(config, "Storage:S3:SecretKey");

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
    /// Reads a required configuration value and throws
    /// <see cref="InvalidOperationException"/> with a clear "must be a
    /// non-empty string" message if the value is null, empty, or whitespace.
    /// Used for credentials and connection settings where a silently-empty
    /// value would otherwise produce an opaque AccessDenied at first use.
    /// </summary>
    private static string RequireNonEmpty(IConfiguration config, string key)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} must be a non-empty string (got: " +
                $"{(value is null ? "null" : "empty/whitespace")}). " +
                "Check the corresponding environment variable in docker/.env.");
        }
        return value;
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
    /// Performs an end-to-end storage probe at startup: HeadBucket → PutObject
    /// → GetObject (verify round-trip body) → DeleteObject. Called once at
    /// boot by <see cref="S3StorageStartupProbe"/>.
    ///
    /// <para>
    /// This is the "tell me my MinIO config is broken in 10 seconds" check —
    /// instead of waiting for the first real user upload to fail with an
    /// opaque 502, we exercise every code path (auth, bucket existence,
    /// upload, download, delete) against a 32-byte diagnostic object stored
    /// at <c>_diagnostics/startup-probe.txt</c>. Each step logs a stage tag
    /// (<c>step 1/4 OK</c> / <c>step 2/4 FAILED</c>) so a single grep of
    /// <c>make prod-logs</c> tells operators exactly which permission /
    /// configuration knob is wrong.
    /// </para>
    ///
    /// <para>
    /// Failures are logged with the full MinIO / S3 error response body
    /// (via <see cref="S3DiagnosticsLogging.BuildErrorContext"/>) but never
    /// rethrown — a credential or DNS bug at boot time should surface as a
    /// loud log line, not crash the API and take the rest of the platform
    /// down with it. The probe always tries to delete its diagnostic object,
    /// even when an earlier step failed, so a flaky boot doesn't leave
    /// junk behind in the bucket.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> if every step (HeadBucket, PutObject, GetObject
    ///     body match, DeleteObject) succeeded; <c>false</c> if any step
    ///     failed — in which case the structured log line names the failing
    ///     stage.</returns>
    public async Task<bool> ProbeBucketAsync(CancellationToken ct = default)
    {
        // Logged at Warning (not Information) so the access-key-prefix summary
        // shows up at prod-default log level (`Logging:LogLevel:Default=Warning`
        // in appsettings.Production.json). When the probe later fails, an
        // operator scanning the logs sees the configured AccessKeyPrefix
        // *before* the cascading step failures — an empty key prefix
        // ("(none)") is the unambiguous "you didn't pass credentials" signal.
        _logger.LogWarning(
            "S3 storage probe starting. Endpoint={Endpoint} Bucket={Bucket} AccessKeyPrefix={AccessKeyPrefix}",
            _serviceUrl,
            _bucketName,
            S3DiagnosticsLogging.FormatAccessKeyPrefix(_accessKey));

        const string probeKey = "_diagnostics/startup-probe.txt";
        // Random body lets us detect the worst-case "probe object existed
        // before, GetObject returned a stale value" scenario — the only way
        // GetObject can return the *expected* body is if our PutObject
        // actually landed in this run.
        var probeBody = Guid.NewGuid().ToString("N");

        // ── Step 1/4: HeadBucket equivalent ────────────────────────────────
        // IAmazonS3 doesn't expose HeadBucketAsync directly (only on the
        // concrete client), so we use GetBucketLocationAsync — same shape
        // (bucket-level read), same failure modes for bad creds / missing
        // bucket, and works under Moq for tests.
        if (!await ProbeStepAsync("step 1/4", "HeadBucket",
                ct => _s3.GetBucketLocationAsync(
                    new GetBucketLocationRequest { BucketName = _bucketName }, ct),
                objectKey: null,
                ct))
        {
            _logger.LogError(
                "S3 storage probe FAILED at step 1/4 (HeadBucket). Endpoint={Endpoint} Bucket={Bucket}",
                _serviceUrl, _bucketName);
            return false;
        }

        // ── Step 2/4: PutObject ────────────────────────────────────────────
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(probeBody);
        var putOk = await ProbeStepAsync("step 2/4", "PutObject",
            async ct =>
            {
                using var ms = new MemoryStream(bodyBytes);
                await _s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName  = _bucketName,
                    Key         = probeKey,
                    InputStream = ms,
                    ContentType = "text/plain",
                    // Same checksum opt-out we use in StoreAsync — MinIO
                    // rejects the SDK's default x-amz-checksum-* header.
                    DisableDefaultChecksumValidation = true,
                }, ct);
            },
            objectKey: probeKey,
            ct);

        if (!putOk)
        {
            _logger.LogError(
                "S3 storage probe FAILED at step 2/4 (PutObject). Endpoint={Endpoint} Bucket={Bucket} Key={Key}",
                _serviceUrl, _bucketName, probeKey);
            // No cleanup needed — nothing was uploaded.
            return false;
        }

        // From here on the probe object exists. Track failures but always
        // run step 4 (delete) in a finally so we don't leak a diagnostic
        // object every time the boot probe partially fails.
        var allSucceeded = true;

        // ── Step 3/4: GetObject + body verify ──────────────────────────────
        try
        {
            using var resp = await _s3.GetObjectAsync(_bucketName, probeKey, ct);
            using var reader = new StreamReader(resp.ResponseStream);
            var got = await reader.ReadToEndAsync(ct);

            if (!string.Equals(got, probeBody, StringComparison.Ordinal))
            {
                _logger.LogError(
                    "S3 storage probe step 3/4 FAILED (GetObject body mismatch). Endpoint={Endpoint} Bucket={Bucket} Key={Key} ExpectedLength={ExpectedLength} ActualLength={ActualLength}",
                    _serviceUrl, _bucketName, probeKey, probeBody.Length, got.Length);
                allSucceeded = false;
            }
            else
            {
                _logger.LogInformation(
                    "S3 storage probe step 3/4 OK (GetObject, body match). Endpoint={Endpoint} Bucket={Bucket} Key={Key}",
                    _serviceUrl, _bucketName, probeKey);
            }
        }
        catch (AmazonS3Exception ex)
        {
            LogS3Failure("S3 storage probe step 3/4 FAILED (GetObject)", ex, probeKey);
            allSucceeded = false;
        }
        catch (Exception ex)
        {
            LogNonS3ProbeFailure(ex, "step 3/4 GetObject", probeKey);
            allSucceeded = false;
        }

        // ── Step 4/4: DeleteObject (always attempt cleanup) ────────────────
        var deleteOk = await ProbeStepAsync("step 4/4", "DeleteObject",
            ct => _s3.DeleteObjectAsync(_bucketName, probeKey, ct),
            objectKey: probeKey,
            ct);
        if (!deleteOk)
            allSucceeded = false;

        if (allSucceeded)
        {
            _logger.LogInformation(
                "S3 storage probe OK. Endpoint={Endpoint} Bucket={Bucket}",
                _serviceUrl, _bucketName);
        }
        else
        {
            _logger.LogError(
                "S3 storage probe FAILED. Endpoint={Endpoint} Bucket={Bucket}",
                _serviceUrl, _bucketName);
        }

        return allSucceeded;
    }

    /// <summary>
    /// Runs a single probe step inside a uniform try / log / return-bool
    /// wrapper so the four-step orchestration above stays readable. Logs
    /// success at Information and any failure (S3 or otherwise) at Error
    /// with the enriched diagnostic payload.
    /// </summary>
    private async Task<bool> ProbeStepAsync(
        string stepLabel,
        string operation,
        Func<CancellationToken, Task> action,
        string? objectKey,
        CancellationToken ct)
    {
        try
        {
            await action(ct);
            if (objectKey is null)
            {
                _logger.LogInformation(
                    "S3 storage probe {Step} OK ({Operation}). Endpoint={Endpoint} Bucket={Bucket}",
                    stepLabel, operation, _serviceUrl, _bucketName);
            }
            else
            {
                _logger.LogInformation(
                    "S3 storage probe {Step} OK ({Operation}). Endpoint={Endpoint} Bucket={Bucket} Key={Key}",
                    stepLabel, operation, _serviceUrl, _bucketName, objectKey);
            }
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            LogS3Failure($"S3 storage probe {stepLabel} FAILED ({operation})", ex, objectKey);
            return false;
        }
        catch (Exception ex)
        {
            LogNonS3ProbeFailure(ex, $"{stepLabel} {operation}", objectKey);
            return false;
        }
    }

    /// <summary>
    /// Logs a probe-stage failure caused by a non-S3 exception (DNS, socket,
    /// TLS handshake, etc.). These can't carry an S3 error code but still
    /// need the same endpoint/bucket/key-prefix context so operators can
    /// distinguish "MinIO replied with an error" from "MinIO was unreachable".
    /// </summary>
    private void LogNonS3ProbeFailure(Exception ex, string stage, string? objectKey)
    {
        _logger.LogError(ex,
            "S3 storage probe failed with non-S3 exception at {Stage}. Endpoint={Endpoint} Bucket={Bucket} AccessKeyPrefix={AccessKeyPrefix} ObjectKey={ObjectKey}",
            stage,
            _serviceUrl,
            _bucketName,
            S3DiagnosticsLogging.FormatAccessKeyPrefix(_accessKey),
            objectKey);
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
