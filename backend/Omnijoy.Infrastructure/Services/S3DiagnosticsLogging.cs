using Amazon.S3;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Helpers for turning an <see cref="AmazonS3Exception"/> into a structured
/// logging payload that includes the underlying MinIO / S3 error response
/// body — the bit the SDK normally hides when it surfaces a generic
/// "Access Denied" message.
///
/// <para>
/// Security guarantees enforced by these helpers:
/// <list type="bullet">
///   <item>The full access key is never returned — only a short prefix +
///         length tag (<see cref="FormatAccessKeyPrefix"/>).</item>
///   <item>The secret key is not accepted as an input at all, so it can't
///         leak by accident.</item>
/// </list>
/// </para>
///
/// <para>
/// Why this helper exists: when MinIO rejects a PutObject the SDK throws
/// <see cref="AmazonS3Exception"/> whose <c>Message</c> is just
/// <c>"Access Denied"</c>. The actual MinIO-side reason
/// (e.g. <c>SignatureDoesNotMatch</c>, <c>InvalidAccessKeyId</c>,
/// <c>NoSuchBucket</c>) sits in <see cref="AmazonS3Exception.ResponseBody"/>.
/// Surfacing that field in the log line means an operator can diagnose a
/// 403 without docker-exec'ing into MinIO to compare timestamps.
/// </para>
/// </summary>
internal static class S3DiagnosticsLogging
{
    /// <summary>
    /// Returns a short, log-safe representation of an access key:
    /// first 4 characters followed by the full length in parentheses
    /// (e.g. <c>"AKIA (20)"</c>). Returns <c>"(none)"</c> when the key is
    /// null or empty, and <c>"({n})"</c> when it's shorter than 4 chars
    /// (so we never accidentally print a complete short key).
    ///
    /// The prefix lets ops cross-reference which credential the backend is
    /// using against MinIO's user list without exposing enough of the key
    /// to be useful to an attacker.
    /// </summary>
    public static string FormatAccessKeyPrefix(string? accessKey)
    {
        if (string.IsNullOrEmpty(accessKey))
            return "(none)";

        var len = accessKey.Length;
        if (len < 4)
            return $"({len})";

        return $"{accessKey[..4]} ({len})";
    }

    /// <summary>
    /// Builds the structured payload that gets emitted alongside the log line
    /// when an S3 call fails. Pulls the MinIO / S3 error response body
    /// (<see cref="AmazonS3Exception.ResponseBody"/>) into a top-level field,
    /// along with the configured endpoint, bucket, and an access-key prefix.
    /// </summary>
    /// <param name="ex">The exception thrown by the AWS SDK.</param>
    /// <param name="endpoint">The S3 endpoint URL the client is targeting.</param>
    /// <param name="bucket">The bucket name the operation was directed at.</param>
    /// <param name="accessKey">The full access key — used only to derive a
    ///     log-safe prefix via <see cref="FormatAccessKeyPrefix"/>. The full
    ///     value is never stored in the returned payload.</param>
    /// <param name="objectKey">Optional S3 object key the operation was
    ///     targeting (null for bucket-level ops like HeadBucket).</param>
    public static IReadOnlyDictionary<string, object?> BuildErrorContext(
        AmazonS3Exception ex,
        string endpoint,
        string bucket,
        string? accessKey,
        string? objectKey = null)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return new Dictionary<string, object?>
        {
            ["ErrorCode"]       = ex.ErrorCode,
            ["ErrorType"]       = ex.ErrorType.ToString(),
            ["StatusCode"]      = (int)ex.StatusCode,
            ["RequestId"]       = ex.RequestId,
            ["AmazonId2"]       = ex.AmazonId2,
            ["ResponseBody"]    = ex.ResponseBody,
            ["Message"]         = ex.Message,
            ["Endpoint"]        = endpoint,
            ["Bucket"]          = bucket,
            ["AccessKeyPrefix"] = FormatAccessKeyPrefix(accessKey),
            ["ObjectKey"]       = objectKey,
        };
    }
}
