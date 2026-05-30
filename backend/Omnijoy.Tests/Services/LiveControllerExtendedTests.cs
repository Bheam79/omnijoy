using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Live;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Extended tests for <see cref="LiveController"/> — ProxyHls action.
/// </summary>
public class LiveControllerExtendedTests
{
    private static (LiveController, Mock<ILiveStreamService>, Mock<IHttpClientFactory>)
        BuildWithHttpClient(
            Mock<System.Net.Http.HttpMessageHandler>? msgHandler = null,
            Guid? userId = null)
    {
        var live    = new Mock<ILiveStreamService>();
        var liveHub = new Mock<IHubContext<LiveHub>>();
        var notifs  = new Mock<INotificationService>();

        var clients = new Mock<IHubClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        liveHub.Setup(h => h.Clients).Returns(clients.Object);

        Mock<IHttpClientFactory> httpFactory = new();
        if (msgHandler != null)
        {
            var client = new System.Net.Http.HttpClient(msgHandler.Object);
            httpFactory.Setup(f => f.CreateClient("mediamtx")).Returns(client);
        }
        else
        {
            // Default: return a client that always times out / fails
            httpFactory.Setup(f => f.CreateClient("mediamtx"))
                       .Returns(new System.Net.Http.HttpClient());
        }

        var config     = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Live:HlsBaseUrl"] = "http://localhost:8888"
            })
            .Build();

        var controller = new LiveController(
            live.Object, liveHub.Object, notifs.Object, httpFactory.Object, config);

        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        // Set up a cancellation token source so HttpContext.RequestAborted works
        http.RequestAborted = CancellationToken.None;
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        return (controller, live, httpFactory);
    }

    private static LiveStreamDto SampleStreamDto(Guid id) => new(
        Id:          id,
        Host:        new LiveStreamHostDto(Guid.NewGuid(), "Host", null),
        Title:       "Stream",
        Status:      "Live",
        Privacy:     "Everyone",
        ViewerCount: 0,
        HlsUrl:      null,
        StartedAt:   DateTime.UtcNow,
        EndedAt:     null
    );

    // ── ProxyHls — access checks ──────────────────────────────────────────────

    [Fact]
    public async Task ProxyHls_ReturnsUnauthorized_WhenNoUserId()
    {
        var (controller, _, _) = BuildWithHttpClient(userId: null);

        var result = await controller.ProxyHls(Guid.NewGuid(), "index.m3u8");

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ProxyHls_ReturnsNotFound_WhenStreamMissing()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var (controller, live, _) = BuildWithHttpClient(userId: userId);
        live.Setup(l => l.GetStreamAsync(streamId, userId))
            .ThrowsAsync(new KeyNotFoundException("not found"));

        var result = await controller.ProxyHls(streamId, "index.m3u8");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ProxyHls_Returns403_WhenNotAuthorized()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var (controller, live, _) = BuildWithHttpClient(userId: userId);
        live.Setup(l => l.GetStreamAsync(streamId, userId))
            .ThrowsAsync(new UnauthorizedAccessException("private"));

        var result = await controller.ProxyHls(streamId, "index.m3u8");

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ProxyHls_ReturnsNotFound_WhenStreamKeyMissing()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var (controller, live, _) = BuildWithHttpClient(userId: userId);
        live.Setup(l => l.GetStreamAsync(streamId, userId))
            .ReturnsAsync(SampleStreamDto(streamId));
        live.Setup(l => l.GetStreamKeyAsync(streamId)).ReturnsAsync((string?)null);

        var result = await controller.ProxyHls(streamId, "index.m3u8");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ProxyHls_ReturnsServiceUnavailable_WhenMediaServerUnreachable()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();

        // Use a mock handler that throws HttpRequestException
        var handler = new Mock<System.Net.Http.HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<System.Net.Http.HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new System.Net.Http.HttpRequestException("connection refused"));

        var (controller, live, _) = BuildWithHttpClient(handler, userId);
        live.Setup(l => l.GetStreamAsync(streamId, userId))
            .ReturnsAsync(SampleStreamDto(streamId));
        live.Setup(l => l.GetStreamKeyAsync(streamId)).ReturnsAsync("stream-key-123");

        var result = await controller.ProxyHls(streamId, "index.m3u8");

        ((ObjectResult)result).StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task ProxyHls_ProxiesContent_WhenMediaServerResponds()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.ByteArrayContent(new byte[] { 0x47 }) // TS packet byte
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp2t");

        var handler = new Mock<System.Net.Http.HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<System.Net.Http.HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var (controller, live, _) = BuildWithHttpClient(handler, userId);
        live.Setup(l => l.GetStreamAsync(streamId, userId))
            .ReturnsAsync(SampleStreamDto(streamId));
        live.Setup(l => l.GetStreamKeyAsync(streamId)).ReturnsAsync("stream-key-123");

        var result = await controller.ProxyHls(streamId, "stream.ts");

        result.Should().BeOfType<FileContentResult>();
        var file = (FileContentResult)result;
        file.ContentType.Should().Be("video/mp2t");
    }
}
