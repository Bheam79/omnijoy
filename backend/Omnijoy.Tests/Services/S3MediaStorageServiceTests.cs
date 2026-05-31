using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnijoy.Infrastructure.Services;
using System.Net;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="S3MediaStorageService"/>.
///
/// Validation paths (empty/too-large/wrong-extension) are exercised without
/// a real S3 endpoint — the validations throw before any network call.
/// The happy-path upload and delete tests use a mocked <see cref="IAmazonS3"/>.
/// </summary>
public class S3MediaStorageServiceTests
{
    private const string BucketName    = "test-bucket";
    private const string PublicBaseUrl = "https://cdn.example.com";
    private const string ServiceUrl    = "http://minio.test:9000";
    private const string AccessKey     = "AKIATESTKEY000000001";

    private static S3MediaStorageService CreateSut(
        IAmazonS3? s3 = null,
        ILogger<S3MediaStorageService>? logger = null)
    {
        var mockS3 = s3 ?? new Mock<IAmazonS3>().Object;
        return new S3MediaStorageService(
            mockS3,
            BucketName,
            PublicBaseUrl,
            logger ?? NullLogger<S3MediaStorageService>.Instance,
            ServiceUrl,
            AccessKey);
    }

    // ── Constructor (config-based) validation ─────────────────────────────────

    [Fact]
    public void Constructor_MissingServiceUrl_ThrowsInvalidOperation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3:BucketName"] = "bucket",
                ["Storage:S3:AccessKey"]  = "key",
                ["Storage:S3:SecretKey"]  = "secret",
            })
            .Build();

        var act = () => new S3MediaStorageService(config, NullLogger<S3MediaStorageService>.Instance);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ServiceUrl*");
    }

    [Fact]
    public void Constructor_MissingBucketName_ThrowsInvalidOperation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3:ServiceUrl"] = "https://s3.example.com",
                ["Storage:S3:AccessKey"]  = "key",
                ["Storage:S3:SecretKey"]  = "secret",
            })
            .Build();

        var act = () => new S3MediaStorageService(config, NullLogger<S3MediaStorageService>.Instance);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*BucketName*");
    }

    [Fact]
    public void Constructor_MissingAccessKey_ThrowsInvalidOperation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3:ServiceUrl"] = "https://s3.example.com",
                ["Storage:S3:BucketName"] = "bucket",
                ["Storage:S3:SecretKey"]  = "secret",
            })
            .Build();

        var act = () => new S3MediaStorageService(config, NullLogger<S3MediaStorageService>.Instance);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*AccessKey*");
    }

    [Fact]
    public void Constructor_MissingSecretKey_ThrowsInvalidOperation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3:ServiceUrl"] = "https://s3.example.com",
                ["Storage:S3:BucketName"] = "bucket",
                ["Storage:S3:AccessKey"]  = "key",
            })
            .Build();

        var act = () => new S3MediaStorageService(config, NullLogger<S3MediaStorageService>.Instance);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*SecretKey*");
    }

    [Fact]
    public void Constructor_AllConfigPresent_UsesPublicBaseUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3:ServiceUrl"]    = "https://s3.example.com",
                ["Storage:S3:BucketName"]    = "mybucket",
                ["Storage:S3:AccessKey"]     = "key",
                ["Storage:S3:SecretKey"]     = "secret",
                ["Storage:S3:PublicBaseUrl"] = "https://cdn.example.com",
            })
            .Build();

        // Should not throw — the real constructor builds an AmazonS3Client
        // with fake credentials (no actual connection is made).
        var act = () => new S3MediaStorageService(config, NullLogger<S3MediaStorageService>.Instance);

        act.Should().NotThrow();
    }

    // ── StoreAsync — validation ───────────────────────────────────────────────

    [Fact]
    public async Task Store_EmptyStream_ThrowsArgumentException()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream(Array.Empty<byte>());

        await sut.Invoking(s => s.StoreAsync(stream, "photo.jpg", "avatars"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task Store_FileTooLarge_ThrowsArgumentException()
    {
        var sut = CreateSut();
        // 6 MB — exceeds the 5 MB cap
        using var stream = new MemoryStream(new byte[6 * 1024 * 1024 + 1]);

        await sut.Invoking(s => s.StoreAsync(stream, "big.jpg", "avatars"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maximum*");
    }

    [Fact]
    public async Task Store_DisallowedExtension_ThrowsArgumentException()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[100]);

        await sut.Invoking(s => s.StoreAsync(stream, "script.js", "avatars"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not allowed*");
    }

    // ── StoreAsync — happy path ───────────────────────────────────────────────

    [Fact]
    public async Task Store_ValidJpeg_ReturnsPublicUrl()
    {
        var s3Mock = new Mock<IAmazonS3>();
        // TransferUtility calls PutObjectAsync for small single-part uploads.
        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var sut = CreateSut(s3Mock.Object);
        using var stream = new MemoryStream(new byte[100]);

        var url = await sut.StoreAsync(stream, "photo.jpg", "avatars");

        url.Should().StartWith($"{PublicBaseUrl}/avatars/");
        url.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task Store_ValidPng_ReturnsCorrectContentUrl()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var sut = CreateSut(s3Mock.Object);
        using var stream = new MemoryStream(new byte[200]);

        var url = await sut.StoreAsync(stream, "image.png", "posts");

        url.Should().EndWith(".png");
    }

    [Fact]
    public async Task Store_S3ThrowsAmazonS3Exception_RethrowsException()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access Denied"));

        var sut = CreateSut(s3Mock.Object);
        using var stream = new MemoryStream(new byte[100]);

        await sut.Invoking(s => s.StoreAsync(stream, "photo.jpg", "avatars"))
            .Should().ThrowAsync<AmazonS3Exception>();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_EmptyUrl_DoesNotCallS3()
    {
        var s3Mock = new Mock<IAmazonS3>();
        var sut = CreateSut(s3Mock.Object);

        await sut.DeleteAsync(string.Empty);

        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_NullUrl_DoesNotCallS3()
    {
        var s3Mock = new Mock<IAmazonS3>();
        var sut = CreateSut(s3Mock.Object);

        await sut.DeleteAsync(null!);

        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_UrlNotMatchingPublicBase_DoesNotCallS3()
    {
        var s3Mock = new Mock<IAmazonS3>();
        var sut = CreateSut(s3Mock.Object);

        await sut.DeleteAsync("https://other.cdn.com/photo.jpg");

        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ValidUrl_CallsS3DeleteObject()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(s => s.DeleteObjectAsync(BucketName, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });

        var sut = CreateSut(s3Mock.Object);
        var objectKey = "avatars/my-avatar.jpg";
        var url = $"{PublicBaseUrl}/{objectKey}";

        await sut.DeleteAsync(url);

        s3Mock.Verify(s => s.DeleteObjectAsync(BucketName, objectKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_S3ThrowsAmazonS3Exception_SilentlyIgnores()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(s => s.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("NoSuchKey"));

        var sut = CreateSut(s3Mock.Object);
        var url = $"{PublicBaseUrl}/avatars/ghost.jpg";

        // DeleteAsync is best-effort — it swallows S3 exceptions.
        var act = () => sut.DeleteAsync(url);

        await act.Should().NotThrowAsync();
    }

    // ── StoreAsync — enriched failure logging ─────────────────────────────────

    [Fact]
    public async Task Store_S3Failure_LogIncludesEnrichedContext()
    {
        // The whole point of OMNIJOY-129: when MinIO rejects an upload, the
        // log line must include the endpoint, bucket, and access-key prefix
        // so an operator can correlate it against the MinIO container without
        // docker-exec'ing in.
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(
                "Access Denied", Amazon.Runtime.ErrorType.Sender,
                "SignatureDoesNotMatch", "REQ-99", HttpStatusCode.Forbidden));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);
        using var stream = new MemoryStream(new byte[100]);

        await sut.Invoking(s => s.StoreAsync(stream, "photo.jpg", "avatars"))
            .Should().ThrowAsync<AmazonS3Exception>();

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("SignatureDoesNotMatch") &&
                    state.ToString()!.Contains(ServiceUrl) &&
                    state.ToString()!.Contains(BucketName) &&
                    state.ToString()!.Contains("AKIA")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Store_S3Failure_FullAccessKeyDoesNotAppearInLog()
    {
        // Cross-cutting guarantee — the secret-leak guard from
        // S3DiagnosticsLoggingTests applies end-to-end too. If a future change
        // to the log line passes the raw access key, this catches it.
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access Denied"));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);
        using var stream = new MemoryStream(new byte[100]);

        await sut.Invoking(s => s.StoreAsync(stream, "photo.jpg", "avatars"))
            .Should().ThrowAsync<AmazonS3Exception>();

        loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(AccessKey)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "full access key must never appear in any log message");
    }

    // ── ProbeBucketAsync — round-trip probe (OMNIJOY-130) ─────────────────────

    /// <summary>
    /// Wires up an IAmazonS3 mock for a fully-successful 4-step probe path.
    /// Pulls the PutObject body bytes out of the latest captured request so
    /// a GetObject stub can echo the exact body back — modelling the
    /// real-storage round-trip behavior.
    /// </summary>
    private static Mock<IAmazonS3> BuildRoundTripMock()
    {
        var s3Mock = new Mock<IAmazonS3>();
        byte[]? lastPut = null;

        s3Mock
            .Setup(s => s.GetBucketLocationAsync(
                It.IsAny<GetBucketLocationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetBucketLocationResponse { HttpStatusCode = HttpStatusCode.OK });

        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) =>
            {
                using var ms = new MemoryStream();
                req.InputStream.Position = 0;
                req.InputStream.CopyTo(ms);
                lastPut = ms.ToArray();
            })
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        s3Mock
            .Setup(s => s.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new GetObjectResponse
            {
                ResponseStream = new MemoryStream(lastPut ?? Array.Empty<byte>()),
                HttpStatusCode = HttpStatusCode.OK,
            });

        s3Mock
            .Setup(s => s.DeleteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });

        return s3Mock;
    }

    [Fact]
    public async Task Probe_AllStepsSucceed_ReturnsTrueAndLogsProbeOK()
    {
        // Round-trip mock: GetObject echoes back the same body that
        // PutObject was given, so the body-match step in step 3 passes.
        var s3Mock = BuildRoundTripMock();
        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();

        var sut = CreateSut(s3Mock.Object, loggerMock.Object);
        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeTrue();

        // Verify every step ran:
        s3Mock.Verify(s => s.GetBucketLocationAsync(
            It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once, "step 1/4 HeadBucket must run");
        s3Mock.Verify(s => s.PutObjectAsync(
            It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Once, "step 2/4 PutObject must run");
        s3Mock.Verify(s => s.GetObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once, "step 3/4 GetObject must run");
        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once, "step 4/4 DeleteObject must run for cleanup");

        // Acceptance criterion: a clear "OK" log line lands within seconds of start.
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("S3 storage probe OK")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Probe_PutObjectUploadsExpectedDiagnosticKey()
    {
        // The diagnostic object MUST land at _diagnostics/startup-probe.txt
        // (per OMNIJOY-130 spec) so operators know exactly which key is the
        // probe artefact when they're poking around in MinIO.
        var s3Mock = BuildRoundTripMock();
        PutObjectRequest? captured = null;

        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var sut = CreateSut(s3Mock.Object);
        await sut.ProbeBucketAsync();

        captured.Should().NotBeNull();
        captured!.BucketName.Should().Be(BucketName);
        captured.Key.Should().Be("_diagnostics/startup-probe.txt");
    }

    [Fact]
    public async Task Probe_HeadBucketFails_ReturnsFalseAndSkipsRemainingSteps()
    {
        // If HeadBucket fails, the probe must not attempt PutObject — that
        // would leak a diagnostic object into a possibly-misconfigured bucket
        // *and* mask the real failure (bad creds) behind a noisier symptom.
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.GetBucketLocationAsync(
                It.IsAny<GetBucketLocationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(
                "Access Denied", Amazon.Runtime.ErrorType.Sender,
                "InvalidAccessKeyId", "REQ-1", HttpStatusCode.Forbidden));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);

        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeFalse();
        s3Mock.Verify(s => s.PutObjectAsync(
            It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never, "PutObject must not be attempted after a HeadBucket failure");

        // Acceptance criterion: failure log includes the enriched error code.
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("step 1/4") &&
                    state.ToString()!.Contains("InvalidAccessKeyId")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Probe_PutObjectFails_ReturnsFalseAndDoesNotAttemptDelete()
    {
        // If PutObject fails the object never exists, so there's nothing to
        // delete — running step 4 would just add a misleading "DeleteObject
        // FAILED (NoSuchKey)" line to the log. Skip cleanup in this case.
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(
                "Access Denied", Amazon.Runtime.ErrorType.Sender,
                "SignatureDoesNotMatch", "REQ-2", HttpStatusCode.Forbidden));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);

        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeFalse();
        s3Mock.Verify(s => s.GetObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no cleanup needed when PutObject failed");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("step 2/4") &&
                    state.ToString()!.Contains("SignatureDoesNotMatch")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Probe_GetObjectBodyMismatch_ReturnsFalseButStillDeletes()
    {
        // GetObject succeeds at the protocol level but returns a different
        // body than we PUT — e.g. a stale value from a previous probe run,
        // or a buggy proxy. Probe must report failure but still clean up.
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new GetObjectResponse
            {
                ResponseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("wrong-body")),
                HttpStatusCode = HttpStatusCode.OK,
            });

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);

        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeFalse();
        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once, "step 4 must still run to clean up the probe object");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("step 3/4") &&
                    state.ToString()!.Contains("body mismatch")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Probe_GetObjectThrows_ReturnsFalseButStillDeletes()
    {
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(
                "Access Denied", Amazon.Runtime.ErrorType.Sender,
                "AccessDenied", "REQ-3", HttpStatusCode.Forbidden));

        var sut = CreateSut(s3Mock.Object);

        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeFalse();
        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once, "cleanup runs even when GetObject throws");
    }

    [Fact]
    public async Task Probe_DeleteObjectFails_ReturnsFalseAndLogsEnriched()
    {
        // Put + Get worked but Delete didn't — e.g. read-only credentials.
        // The probe still reports failure (the object is now leaked) and the
        // log line carries the underlying error code.
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.DeleteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(
                "Access Denied", Amazon.Runtime.ErrorType.Sender,
                "AccessDenied", "REQ-4", HttpStatusCode.Forbidden));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);

        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeFalse();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("step 4/4") &&
                    state.ToString()!.Contains("AccessDenied")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Probe_NonS3ExceptionDuringHeadBucket_ReturnsFalseAndLogs()
    {
        // DNS / socket / TLS errors don't map to AmazonS3Exception. The probe
        // must still log and return false rather than crashing the host.
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.GetBucketLocationAsync(
                It.IsAny<GetBucketLocationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Name or service not known"));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);

        var ok = await sut.ProbeBucketAsync();

        ok.Should().BeFalse();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("non-S3 exception") &&
                    state.ToString()!.Contains("step 1/4")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Probe_AnyStepFailure_ProbeFailedLineAlwaysIncludesEndpointAndBucket()
    {
        // Smoke test of the acceptance log shape: "S3 storage probe FAILED.
        // Endpoint=... Bucket=..." — operators grep for this line first.
        var s3Mock = BuildRoundTripMock();
        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access Denied"));

        var loggerMock = new Mock<ILogger<S3MediaStorageService>>();
        var sut = CreateSut(s3Mock.Object, loggerMock.Object);

        await sut.ProbeBucketAsync();

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("S3 storage probe FAILED") &&
                    state.ToString()!.Contains(ServiceUrl) &&
                    state.ToString()!.Contains(BucketName)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
