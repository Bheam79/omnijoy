using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Omnijoy.Api.HealthChecks;

/// <summary>
/// Readiness probe for the MinIO (S3-compatible) object store.
///
/// <para>
/// Implementation notes:
/// </para>
/// <list type="bullet">
///   <item>
///     Calls <c>GetBucketLocationAsync</c> — a lightweight, head-style request
///     that confirms the bucket exists and the credentials authenticate. It is
///     intentionally cheaper than the full end-to-end round trip done by
///     <c>S3StorageStartupProbe</c> (which uploads + downloads + deletes a
///     diagnostic object once at boot). The readiness endpoint can be polled
///     several times per minute by orchestrators / load balancers, so a
///     PUT/GET/DELETE every probe would be wasteful.
///   </item>
///   <item>
///     The result is cached in <see cref="IMemoryCache"/> for 10 seconds so a
///     burst of probe requests does not translate into a burst of S3 calls.
///     This is a deliberately cheap cache layer — not the
///     <c>HealthChecks.CachedHealthCheck</c> NuGet package — to keep the
///     dependency surface small.
///   </item>
///   <item>
///     Never throws. Any <see cref="AmazonS3Exception"/> (or unexpected error)
///     is converted to <see cref="HealthCheckResult.Unhealthy(string?, Exception?, IReadOnlyDictionary{string, object}?)"/>
///     with a description so the readiness payload tells the operator which
///     subsystem is degraded.
///   </item>
/// </list>
/// </summary>
public sealed class MinioHealthCheck : IHealthCheck
{
    internal const string CacheKey = "healthcheck:minio";
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    private readonly IAmazonS3 _s3;
    private readonly IMemoryCache _cache;
    private readonly string _bucketName;

    public MinioHealthCheck(IAmazonS3 s3, IConfiguration config, IMemoryCache cache)
    {
        _s3         = s3 ?? throw new ArgumentNullException(nameof(s3));
        _cache      = cache ?? throw new ArgumentNullException(nameof(cache));
        _bucketName = config["Storage:S3:BucketName"]
            ?? throw new InvalidOperationException(
                "Storage:S3:BucketName must be configured for the MinIO health check.");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Cached result avoids hammering S3 when the readiness endpoint is
        // polled aggressively (LB/orchestrator probes can run several times
        // per second across replicas).
        if (_cache.TryGetValue<HealthCheckResult>(CacheKey, out var cached))
        {
            return cached;
        }

        HealthCheckResult result;
        try
        {
            // GetBucketLocation is a cheap metadata call — no objects fetched,
            // no body in the response. Throws AmazonS3Exception on auth or
            // bucket-existence failures.
            await _s3.GetBucketLocationAsync(_bucketName, cancellationToken)
                     .ConfigureAwait(false);

            result = HealthCheckResult.Healthy(
                $"MinIO bucket '{_bucketName}' is reachable.");
        }
        catch (AmazonS3Exception ex)
        {
            result = HealthCheckResult.Unhealthy(
                $"MinIO bucket '{_bucketName}' is unreachable: {ex.ErrorCode ?? "S3Error"}.",
                ex);
        }
        catch (Exception ex)
        {
            result = HealthCheckResult.Unhealthy(
                $"MinIO health check failed: {ex.GetType().Name}.",
                ex);
        }

        _cache.Set(CacheKey, result, CacheTtl);
        return result;
    }
}
