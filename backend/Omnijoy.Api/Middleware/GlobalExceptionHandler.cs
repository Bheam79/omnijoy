using System.Security.Claims;
using Amazon.S3;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Omnijoy.Api.Middleware;

/// <summary>
/// Catches every unhandled exception that escapes a controller / SignalR hub
/// and produces a consistent JSON error response.
///
/// The default ASP.NET Core pipeline silently returns 500 with an empty body
/// when an exception escapes — which makes investigating production 5xx reports
/// like "image post returns 500" very hard, because the operator sees no trace
/// of the failure in the logs unless the framework's `Microsoft.AspNetCore.*`
/// loggers are enabled at Information (they default to Warning in production).
///
/// This handler always logs at <see cref="LogLevel.Error"/> with the request
/// path, method, status code and authenticated user id so the failure is
/// captured even with the default log configuration.
///
/// It also maps a small set of known "infrastructure is down" exception types
/// to more accurate HTTP status codes:
///
///   * <see cref="AmazonS3Exception"/> — MinIO / S3 upload failure
///     (likely cause of the "Prod still get 500 on image post / something
///     related to docker services" report tracked under OMNIJOY-125):
///     returned as **502 Bad Gateway** so the client can distinguish a backend
///     bug from a transient storage outage and retry.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapException(exception);

        var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path} (user={UserId}); responding with {StatusCode} {Title}.",
            httpContext.Request.Method,
            httpContext.Request.Path,
            userId ?? "anonymous",
            statusCode,
            title);

        // If the response has already started streaming there's nothing we can
        // do — just let the framework close the connection.
        if (httpContext.Response.HasStarted)
            return true;

        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title  = title,
            Detail = exception.Message,
            Type   = $"https://httpstatuses.io/{statusCode}",
            Instance = httpContext.Request.Path,
        };

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            cancellationToken: cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title) MapException(Exception exception) =>
        exception switch
        {
            // Storage backend (MinIO / S3) is the most common "docker services
            // are flaky" cause of post-upload 500s in production.
            AmazonS3Exception => (
                StatusCodes.Status502BadGateway,
                "Media storage temporarily unavailable."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An internal server error occurred."),
        };
}
