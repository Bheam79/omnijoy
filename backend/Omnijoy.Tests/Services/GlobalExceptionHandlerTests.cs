using System.Net;
using System.Text.Json;
using Amazon.S3;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Omnijoy.Api.Middleware;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="GlobalExceptionHandler"/> — the catch-all middleware
/// added in OMNIJOY-125 to make unhandled exceptions visible (logged with the
/// failing path / user) and to convert known infrastructure outages like
/// MinIO/S3 being unavailable into 502 Bad Gateway responses instead of an
/// opaque 500.
/// </summary>
public class GlobalExceptionHandlerTests
{
    private static DefaultHttpContext BuildContext(
        string method = "POST",
        string path = "/api/posts")
    {
        var http = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };
        http.Request.Method = method;
        http.Request.Path   = path;
        return http;
    }

    private static GlobalExceptionHandler BuildHandler() =>
        new(NullLogger<GlobalExceptionHandler>.Instance);

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpContext http)
    {
        http.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(
            http.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsTrue_ForUnknownException()
    {
        var handler = BuildHandler();
        var http    = BuildContext();

        var handled = await handler.TryHandleAsync(http, new InvalidOperationException("boom"), CancellationToken.None);

        handled.Should().BeTrue();
        http.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);

        var problem = await ReadProblemAsync(http);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.InternalServerError);
        problem.Title.Should().Be("An internal server error occurred.");
        problem.Detail.Should().Be("boom");
    }

    [Fact]
    public async Task TryHandleAsync_MapsAmazonS3Exception_To502BadGateway()
    {
        var handler = BuildHandler();
        var http    = BuildContext();

        var s3ex = new AmazonS3Exception("MinIO is gone");

        var handled = await handler.TryHandleAsync(http, s3ex, CancellationToken.None);

        handled.Should().BeTrue();
        http.Response.StatusCode.Should().Be((int)HttpStatusCode.BadGateway);

        var problem = await ReadProblemAsync(http);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadGateway);
        problem.Title.Should().Be("Media storage temporarily unavailable.");
        problem.Detail.Should().Be("MinIO is gone");
    }

    [Fact]
    public async Task TryHandleAsync_StopsWriting_WhenResponseHasAlreadyStarted()
    {
        // When the response has already started we can't change status / body,
        // but the handler must still return true so the framework doesn't try
        // to emit its own default error page on top of partial output.
        var handler = BuildHandler();

        var http = new DefaultHttpContext();
        http.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(
            new HasStartedResponseFeature { HasStarted = true });
        http.Request.Method = "POST";
        http.Request.Path   = "/api/posts";

        var handled = await handler.TryHandleAsync(http, new Exception("late"), CancellationToken.None);

        handled.Should().BeTrue();
        // No status mutation expected when HasStarted is true.
        http.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task TryHandleAsync_AnonymousRequest_Succeeds()
    {
        // No authenticated user — must not throw on the ClaimsPrincipal lookup.
        var handler = BuildHandler();
        var http    = BuildContext(method: "GET", path: "/api/feed");

        var handled = await handler.TryHandleAsync(http, new Exception("oops"), CancellationToken.None);

        handled.Should().BeTrue();
        http.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Replacement <see cref="Microsoft.AspNetCore.Http.Features.IHttpResponseFeature"/>
    /// whose <c>HasStarted</c> flag we can flip — the default feature in
    /// <see cref="DefaultHttpContext"/> always reports <c>false</c>.
    /// </summary>
    private sealed class HasStartedResponseFeature : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
    {
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public string? ReasonPhrase { get; set; }
        public int StatusCode { get; set; } = 200;
        public void OnCompleted(Func<object, Task> callback, object state) { }
        public void OnStarting(Func<object, Task> callback, object state) { }
    }
}
