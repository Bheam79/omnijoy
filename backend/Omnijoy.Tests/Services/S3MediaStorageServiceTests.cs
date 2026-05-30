using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
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

    private static S3MediaStorageService CreateSut(IAmazonS3? s3 = null)
    {
        var mockS3 = s3 ?? new Mock<IAmazonS3>().Object;
        return new S3MediaStorageService(
            mockS3,
            BucketName,
            PublicBaseUrl,
            NullLogger<S3MediaStorageService>.Instance);
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
}
