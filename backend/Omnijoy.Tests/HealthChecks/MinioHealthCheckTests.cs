using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Omnijoy.Api.HealthChecks;
using System.Net;

namespace Omnijoy.Tests.HealthChecks;

/// <summary>
/// Unit tests for <see cref="MinioHealthCheck"/>.
///
/// The check uses a 10s in-memory cache to avoid hammering S3 on every probe
/// request, and converts <see cref="AmazonS3Exception"/> into an Unhealthy
/// result (rather than letting the exception escape the readiness endpoint).
/// </summary>
public class MinioHealthCheckTests
{
    private const string BucketName = "omnijoy-test";

    private static IConfiguration BuildConfig(string? bucket = BucketName)
    {
        var dict = new Dictionary<string, string?>();
        if (bucket is not null) dict["Storage:S3:BucketName"] = bucket;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static MinioHealthCheck CreateSut(IAmazonS3 s3, IMemoryCache? cache = null)
        => new(s3, BuildConfig(), cache ?? new MemoryCache(new MemoryCacheOptions()));

    // ── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullAmazonS3_Throws()
    {
        var act = () => new MinioHealthCheck(
            null!, BuildConfig(), new MemoryCache(new MemoryCacheOptions()));

        act.Should().Throw<ArgumentNullException>().WithParameterName("s3");
    }

    [Fact]
    public void Constructor_NullMemoryCache_Throws()
    {
        var act = () => new MinioHealthCheck(new Mock<IAmazonS3>().Object, BuildConfig(), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_MissingBucketName_ThrowsInvalidOperation()
    {
        var act = () => new MinioHealthCheck(
            new Mock<IAmazonS3>().Object,
            BuildConfig(bucket: null),
            new MemoryCache(new MemoryCacheOptions()));

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*BucketName*");
    }

    // ── Healthy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_BucketReachable_ReturnsHealthy()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetBucketLocationResponse { HttpStatusCode = HttpStatusCode.OK });

        var sut = CreateSut(s3.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain(BucketName);
        s3.Verify(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Unhealthy: S3 exception ──────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_AmazonS3Exception_ReturnsUnhealthyWithErrorCode()
    {
        var s3 = new Mock<IAmazonS3>();
        var s3Ex = new AmazonS3Exception("Access Denied")
        {
            ErrorCode      = "SignatureDoesNotMatch",
            StatusCode     = HttpStatusCode.Forbidden,
        };
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ThrowsAsync(s3Ex);

        var sut = CreateSut(s3.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("SignatureDoesNotMatch");
        result.Exception.Should().BeSameAs(s3Ex);
    }

    [Fact]
    public async Task CheckHealthAsync_AmazonS3Exception_NoErrorCode_FallsBackToGenericLabel()
    {
        var s3 = new Mock<IAmazonS3>();
        // ErrorCode left as null — exercises the "?? "S3Error"" fallback.
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ThrowsAsync(new AmazonS3Exception("network blip"));

        var sut = CreateSut(s3.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("S3Error");
    }

    // ── Unhealthy: unexpected exception ──────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_UnexpectedException_ReturnsUnhealthy()
    {
        var s3 = new Mock<IAmazonS3>();
        var ex = new InvalidOperationException("DNS resolution failed");
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ThrowsAsync(ex);

        var sut = CreateSut(s3.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain(nameof(InvalidOperationException));
        result.Exception.Should().BeSameAs(ex);
    }

    // ── Caching behaviour ────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_CallsS3OnlyOnceWithinCacheTtl()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetBucketLocationResponse { HttpStatusCode = HttpStatusCode.OK });

        var sut = CreateSut(s3.Object);

        // First call — populates the cache.
        var first = await sut.CheckHealthAsync(new HealthCheckContext());
        // Subsequent calls within the 10s TTL — must reuse the cached result.
        var second = await sut.CheckHealthAsync(new HealthCheckContext());
        var third  = await sut.CheckHealthAsync(new HealthCheckContext());

        first.Status.Should().Be(HealthStatus.Healthy);
        second.Status.Should().Be(HealthStatus.Healthy);
        third.Status.Should().Be(HealthStatus.Healthy);

        s3.Verify(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()),
            Times.Once, "cache should prevent additional S3 calls within the 10s TTL");
    }

    [Fact]
    public async Task CheckHealthAsync_CachesUnhealthyResult()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ThrowsAsync(new AmazonS3Exception("Access Denied") { ErrorCode = "InvalidAccessKeyId" });

        var sut = CreateSut(s3.Object);

        var first  = await sut.CheckHealthAsync(new HealthCheckContext());
        var second = await sut.CheckHealthAsync(new HealthCheckContext());

        first.Status.Should().Be(HealthStatus.Unhealthy);
        second.Status.Should().Be(HealthStatus.Unhealthy);

        // Even for failure responses the cache prevents a stampede.
        s3.Verify(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterCacheEviction_CallsS3Again()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetBucketLocationResponse { HttpStatusCode = HttpStatusCode.OK });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut   = new MinioHealthCheck(s3.Object, BuildConfig(), cache);

        await sut.CheckHealthAsync(new HealthCheckContext());

        // Simulate the 10s TTL expiry without sleeping in the test.
        cache.Remove(MinioHealthCheck.CacheKey);

        await sut.CheckHealthAsync(new HealthCheckContext());

        s3.Verify(s => s.GetBucketLocationAsync(BucketName, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public void CacheTtl_Is10Seconds()
    {
        // The 10s TTL is an explicit acceptance criterion of the task; lock it
        // in with a test so a future tweak shows up as a deliberate change
        // instead of slipping through code review unnoticed.
        MinioHealthCheck.CacheTtl.Should().Be(TimeSpan.FromSeconds(10));
    }
}
