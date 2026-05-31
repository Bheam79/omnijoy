using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// One-shot background service that runs <see cref="S3MediaStorageService.ProbeBucketAsync"/>
/// shortly after application startup. The probe verifies the configured
/// MinIO / S3 endpoint, bucket, and credentials are wired up correctly —
/// when they're not, the resulting log line carries the underlying error
/// (e.g. <c>SignatureDoesNotMatch</c>) rather than the SDK's opaque
/// <c>"Access Denied"</c>, so the failure is visible in
/// <c>make prod-logs</c> without any manual correlation.
///
/// <para>
/// Registered only when <c>Storage:Type == "s3"</c>. Failures are logged but
/// never thrown — startup must succeed even if storage is currently down.
/// </para>
/// </summary>
public sealed class S3StorageStartupProbe : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<S3StorageStartupProbe> _logger;

    public S3StorageStartupProbe(
        IServiceScopeFactory scopeFactory,
        ILogger<S3StorageStartupProbe> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IMediaStorageService>();

            if (storage is S3MediaStorageService s3)
            {
                await s3.ProbeBucketAsync(stoppingToken);
            }
            else
            {
                _logger.LogDebug(
                    "S3StorageStartupProbe registered but IMediaStorageService is {Type}, skipping.",
                    storage.GetType().Name);
            }
        }
        catch (OperationCanceledException)
        {
            // App is shutting down — silent exit.
        }
        catch (Exception ex)
        {
            // Probe failures are already logged inside ProbeBucketAsync; this
            // catch is defensive against DI / scope-resolution problems so we
            // never take down the host with an unobserved background task.
            _logger.LogError(ex, "S3StorageStartupProbe failed unexpectedly.");
        }
    }
}
