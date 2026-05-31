using Amazon.Runtime;
using Amazon.S3;
using FluentAssertions;
using Omnijoy.Infrastructure.Services;
using System.Net;
using System.Reflection;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for the S3 diagnostics logging helper that turns an
/// <see cref="AmazonS3Exception"/> into a structured payload — the bit that
/// turns "Access Denied" into "SignatureDoesNotMatch + endpoint + bucket".
/// The critical invariant: the full access key and the secret key must
/// NEVER appear verbatim in the payload.
/// </summary>
public class S3DiagnosticsLoggingTests
{
    // ── FormatAccessKeyPrefix ─────────────────────────────────────────────────

    [Fact]
    public void FormatAccessKeyPrefix_Null_ReturnsNone()
    {
        S3DiagnosticsLogging.FormatAccessKeyPrefix(null).Should().Be("(none)");
    }

    [Fact]
    public void FormatAccessKeyPrefix_Empty_ReturnsNone()
    {
        S3DiagnosticsLogging.FormatAccessKeyPrefix(string.Empty).Should().Be("(none)");
    }

    [Theory]
    [InlineData("a",   "(1)")]
    [InlineData("ab",  "(2)")]
    [InlineData("abc", "(3)")]
    public void FormatAccessKeyPrefix_ShortKey_HidesEntireKey(string key, string expected)
    {
        // For very short keys we hide the value entirely — printing all 3
        // characters of a 3-char key is just leaking the secret.
        S3DiagnosticsLogging.FormatAccessKeyPrefix(key).Should().Be(expected);
    }

    [Fact]
    public void FormatAccessKeyPrefix_LongKey_ReturnsPrefixAndLength()
    {
        // 20 chars — the AWS standard access-key length
        var key = "AKIAEXAMPLE123456789";
        var formatted = S3DiagnosticsLogging.FormatAccessKeyPrefix(key);

        formatted.Should().Be("AKIA (20)");
        formatted.Should().NotContain(key); // full key must never appear
        formatted.Should().NotContain("EXAMPLE");
    }

    [Fact]
    public void FormatAccessKeyPrefix_MinioStyleKey_ReturnsFirstFourCharsOnly()
    {
        var key = "minioadmin"; // 10 chars, MinIO default root user
        var formatted = S3DiagnosticsLogging.FormatAccessKeyPrefix(key);

        formatted.Should().Be("mini (10)");
        formatted.Should().NotContain("admin");
    }

    // ── BuildErrorContext ─────────────────────────────────────────────────────

    private static AmazonS3Exception MakeException(
        string message = "Access Denied",
        string errorCode = "AccessDenied",
        ErrorType errorType = ErrorType.Sender,
        string requestId = "REQ-123",
        HttpStatusCode statusCode = HttpStatusCode.Forbidden,
        string? responseBody = null)
    {
        var ex = new AmazonS3Exception(message, errorType, errorCode, requestId, statusCode);

        if (responseBody is not null)
        {
            // ResponseBody has a non-public setter — we reach in via reflection
            // so we can verify the helper surfaces the XML payload that MinIO
            // returns. This is the field that actually carries the useful
            // "why was this denied" reason from the server.
            var prop = typeof(AmazonS3Exception).GetProperty(
                nameof(AmazonS3Exception.ResponseBody),
                BindingFlags.Instance | BindingFlags.Public);
            prop!.GetSetMethod(nonPublic: true)!.Invoke(ex, new object?[] { responseBody });
        }

        return ex;
    }

    [Fact]
    public void BuildErrorContext_NullException_Throws()
    {
        var act = () => S3DiagnosticsLogging.BuildErrorContext(
            null!, "http://minio:9000", "bucket", "AKIAEXAMPLE123456789");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildErrorContext_AllFields_PopulatedFromException()
    {
        var ex = MakeException(
            message: "The request signature we calculated does not match.",
            errorCode: "SignatureDoesNotMatch",
            errorType: ErrorType.Sender,
            requestId: "MINIO-REQ-42",
            statusCode: HttpStatusCode.Forbidden,
            responseBody: "<Error><Code>SignatureDoesNotMatch</Code><Message>foo</Message></Error>");

        var ctx = S3DiagnosticsLogging.BuildErrorContext(
            ex,
            endpoint: "http://07ad0b82_omnijoy_minio:9000",
            bucket:   "omnijoy",
            accessKey: "AKIAEXAMPLE123456789",
            objectKey: "avatars/abc.jpg");

        ctx["ErrorCode"].Should().Be("SignatureDoesNotMatch");
        ctx["ErrorType"].Should().Be("Sender");
        ctx["StatusCode"].Should().Be(403);
        ctx["RequestId"].Should().Be("MINIO-REQ-42");
        // AmazonS3Exception.Message getter augments the message with the
        // response body when one is present, so we just assert containment.
        ctx["Message"].Should().NotBeNull();
        ctx["Message"]!.ToString().Should().Contain("The request signature we calculated does not match.");
        ctx["ResponseBody"].Should().Be("<Error><Code>SignatureDoesNotMatch</Code><Message>foo</Message></Error>");
        ctx["Endpoint"].Should().Be("http://07ad0b82_omnijoy_minio:9000");
        ctx["Bucket"].Should().Be("omnijoy");
        ctx["AccessKeyPrefix"].Should().Be("AKIA (20)");
        ctx["ObjectKey"].Should().Be("avatars/abc.jpg");
    }

    [Fact]
    public void BuildErrorContext_NullResponseBody_DoesNotThrow()
    {
        // ResponseBody is often null when the SDK couldn't capture it (network
        // failure mid-stream, AWS SDK signing failure before request was sent,
        // etc.). The helper must still produce a payload.
        var ex = MakeException(responseBody: null);

        var ctx = S3DiagnosticsLogging.BuildErrorContext(
            ex, "http://minio:9000", "bucket", "AKIAEXAMPLE123456789");

        ctx["ResponseBody"].Should().BeNull();
        ctx["ErrorCode"].Should().Be("AccessDenied");
    }

    [Fact]
    public void BuildErrorContext_NoObjectKey_OmitsObjectKey()
    {
        var ex = MakeException();

        var ctx = S3DiagnosticsLogging.BuildErrorContext(
            ex, "http://minio:9000", "bucket", "AKIAEXAMPLE123456789");

        ctx["ObjectKey"].Should().BeNull();
    }

    // ── Secret-leak guard ─────────────────────────────────────────────────────

    [Fact]
    public void BuildErrorContext_FullAccessKey_NeverAppearsInPayload()
    {
        var fullKey = "AKIAEXAMPLE123456789";
        var ex = MakeException();

        var ctx = S3DiagnosticsLogging.BuildErrorContext(
            ex, "http://minio:9000", "bucket", fullKey, objectKey: "avatars/abc.jpg");

        // Scan every value in the dictionary; the full access key must not
        // appear in any field. The prefix tag includes "AKIA" but that's only
        // 4 of the 20 characters — substring(fullKey) would still fail.
        foreach (var kvp in ctx)
        {
            var rendered = kvp.Value?.ToString() ?? string.Empty;
            rendered.Should().NotContain(fullKey,
                because: $"the full access key must never appear in any logged field (field={kvp.Key})");
        }
    }

    [Fact]
    public void BuildErrorContext_PayloadHasNoFieldNamedSecretOrAccessKey()
    {
        // Defence-in-depth: even if a future maintainer adds a "SecretKey"
        // field to the helper signature, this test makes sure they have to
        // delete this assertion to do it (and notice they're about to log a
        // secret).
        var ex = MakeException();

        var ctx = S3DiagnosticsLogging.BuildErrorContext(
            ex, "http://minio:9000", "bucket", "AKIAEXAMPLE123456789");

        ctx.Should().NotContainKey("SecretKey");
        ctx.Should().NotContainKey("AccessKey");
        ctx.Should().NotContainKey("AwsSecretAccessKey");
    }
}
