using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Moq.Protected;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Chat;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MetaPreviewController"/>. Uses a mock
/// HttpMessageHandler to simulate external URL fetches without real network I/O.
/// </summary>
public class MetaPreviewControllerTests : IDisposable
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    // ── Builder helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a no-op resolver mock that returns an empty IP array for any host
    /// (used for tests whose URLs are IP literals — the controller short-circuits
    /// before calling the resolver — and for hostname tests that don't need DNS
    /// interception).
    /// </summary>
    private static Mock<IHostResolver> CreateNoOpResolverMock()
    {
        var mock = new Mock<IHostResolver>();
        mock.Setup(r => r.GetHostAddressesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IPAddress>());
        return mock;
    }

    /// <summary>
    /// Returns a resolver mock that resolves any host to 93.184.216.34
    /// (example.com's real IP — not in any reserved range), so the SSRF check
    /// passes and the test reaches the HTTP-fetch stage.
    /// </summary>
    private static Mock<IHostResolver> StubPublicResolver()
    {
        var mock = new Mock<IHostResolver>();
        mock.Setup(r => r.GetHostAddressesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse("93.184.216.34") });
        return mock;
    }

    private MetaPreviewController Build(
        HttpResponseMessage? response = null,
        bool throwOnFetch = false,
        Mock<IHostResolver>? resolver = null)
    {
        var handler = new Mock<HttpMessageHandler>();

        if (throwOnFetch)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("network error"));
        }
        else if (response != null)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(new HttpClient(handler.Object));

        var resolverInstance = (resolver ?? CreateNoOpResolverMock()).Object;
        var controller = new MetaPreviewController(factory.Object, _cache, resolverInstance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static HttpResponseMessage HtmlResponse(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

    // ── Bad request paths ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlIsNull()
    {
        var controller = Build();

        var result = await controller.GetPreview(null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlIsWhitespace()
    {
        var controller = Build();

        var result = await controller.GetPreview("   ");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlIsRelative()
    {
        var controller = Build();

        var result = await controller.GetPreview("/relative/path");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlIsNotHttp()
    {
        var controller = Build();

        var result = await controller.GetPreview("ftp://example.com/file.zip");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlPointsToLocalhost()
    {
        var controller = Build();

        var result = await controller.GetPreview("http://localhost:5000/api/secret");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlPointsToLoopback()
    {
        var controller = Build();

        var result = await controller.GetPreview("https://127.0.0.1/admin");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlPointsToPrivateNetwork()
    {
        var controller = Build();

        var result = await controller.GetPreview("http://192.168.1.1/internal");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenUrlPointsTo10xNetwork()
    {
        var controller = Build();

        var result = await controller.GetPreview("http://10.0.0.1/secret");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SSRF — new blocked IPv4 ranges ────────────────────────────────────────

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]  // AWS instance metadata
    [InlineData("http://169.254.0.1/")]                       // APIPA generic
    [InlineData("http://100.64.0.1/")]                        // RFC 6598 CGN
    [InlineData("http://100.127.255.255/")]                   // RFC 6598 top of range
    public async Task GetPreview_ReturnsBadRequest_WhenUrlPointsToNewReservedRange(string url)
    {
        var controller = Build();

        var result = await controller.GetPreview(url);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SSRF — IPv6 blocked ranges ────────────────────────────────────────────

    [Theory]
    [InlineData("http://[::1]/admin")]                        // IPv6 loopback
    [InlineData("http://[fc00::1]/internal")]                 // IPv6 ULA fc00::/7
    [InlineData("http://[fdff:ffff:ffff:ffff::1]/x")]        // IPv6 ULA top of fd00::/8
    [InlineData("http://[fe80::1]/link-local")]               // IPv6 link-local
    public async Task GetPreview_ReturnsBadRequest_WhenUrlPointsToReservedIpv6(string url)
    {
        var controller = Build();

        var result = await controller.GetPreview(url);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DNS rebinding ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    public async Task GetPreview_ReturnsBadRequest_WhenHostnameResolvesToPrivateIp(string resolvedIp)
    {
        // Arrange: a public-looking hostname that resolves to an internal IP
        var resolver = new Mock<IHostResolver>();
        resolver
            .Setup(r => r.GetHostAddressesAsync("evil-rebind.example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse(resolvedIp) });

        var controller = Build(resolver: resolver);

        // Act
        var result = await controller.GetPreview("http://evil-rebind.example.com/secret");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DNS resolution failure ────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_ReturnsBadRequest_WhenDnsResolutionFails()
    {
        var resolver = new Mock<IHostResolver>();
        resolver
            .Setup(r => r.GetHostAddressesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException());

        var controller = Build(resolver: resolver);

        var result = await controller.GetPreview("http://nonexistent.invalid/");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Named HttpClient ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_UsesNamedMetaPreviewHttpClient()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(HtmlResponse("<html><head><title>T</title></head></html>"));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("MetaPreview")).Returns(new HttpClient(handler.Object));

        var resolver = StubPublicResolver();
        var ctrl = new MetaPreviewController(factory.Object, _cache, resolver.Object);
        ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        await ctrl.GetPreview("https://example.com/verify-client-name");

        factory.Verify(f => f.CreateClient("MetaPreview"), Times.Once);
    }

    // ── 128 KB truncation guard ───────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_TruncatesAt128KB_AndStillParsesTagsInFirstBlock()
    {
        // Build an HTML string with og:title in the first 128 KB and junk after
        var sb = new StringBuilder();
        sb.Append("""<html><head><meta property="og:title" content="Truncated Title" /></head><body>""");
        sb.Append(new string('x', 200 * 1024)); // 200 KB of junk
        sb.Append("</body></html>");

        var controller = Build(HtmlResponse(sb.ToString()));

        var result = await controller.GetPreview("https://example.com/huge-page");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        // og:title appears before the 128 KB boundary so it must be parsed
        dto.Title.Should().Be("Truncated Title");
    }

    // ── Cache hit ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_ReturnsCachedResult_WhenCacheHit()
    {
        var url      = "https://example.com/page";
        var cacheKey = $"ogpreview:{url}";
        var cached   = new OgPreviewDto(url, "Cached Title", null, null, null);
        _cache.Set(cacheKey, cached);

        var controller = Build(); // No HttpClient setup needed

        var result = await controller.GetPreview(url);

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().Be("Cached Title");
    }

    // ── Non-success HTTP responses ────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_ReturnsEmptyPreview_WhenRemoteReturnsNotFound()
    {
        var response   = new HttpResponseMessage(HttpStatusCode.NotFound);
        var controller = Build(response);

        var result = await controller.GetPreview("https://example.com/missing");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().BeNull();
    }

    [Fact]
    public async Task GetPreview_ReturnsEmptyPreview_WhenContentIsNotHtml()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":true}", Encoding.UTF8, "application/json")
        };
        var controller = Build(response);

        var result = await controller.GetPreview("https://api.example.com/data");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().BeNull();
    }

    // ── Network errors ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_ReturnsEmptyPreview_OnNetworkError()
    {
        var controller = Build(throwOnFetch: true);

        var result = await controller.GetPreview("https://unreachable.example.com/");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().BeNull();
    }

    // ── OG tag parsing ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_ParsesOgTags_WhenHtmlContainsOgMeta()
    {
        var html = """
            <html>
            <head>
              <title>Fallback Title</title>
              <meta property="og:title" content="OG Title" />
              <meta property="og:description" content="OG Desc" />
              <meta property="og:image" content="https://img.example.com/photo.jpg" />
              <meta property="og:site_name" content="Example Site" />
            </head>
            <body>Content</body>
            </html>
            """;
        var controller = Build(HtmlResponse(html));

        var result = await controller.GetPreview("https://example.com/article");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().Be("OG Title");
        dto.Description.Should().Be("OG Desc");
        dto.ImageUrl.Should().Be("https://img.example.com/photo.jpg");
        dto.SiteName.Should().Be("Example Site");
    }

    [Fact]
    public async Task GetPreview_FallsBackToTitleElement_WhenNoOgTitle()
    {
        var html = """
            <html><head><title>Page Title</title></head><body>Hello</body></html>
            """;
        var controller = Build(HtmlResponse(html));

        var result = await controller.GetPreview("https://example.com/page");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().Be("Page Title");
    }

    [Fact]
    public async Task GetPreview_ParsesReverseAttributeOrder_InMetaTags()
    {
        // content before property
        var html = """
            <html><head>
            <meta content="Reversed Title" property="og:title" />
            </head></html>
            """;
        var controller = Build(HtmlResponse(html));

        var result = await controller.GetPreview("https://example.com/reversed");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().Be("Reversed Title");
    }

    [Fact]
    public async Task GetPreview_ParsesTwitterCardTags_WhenOgTagsMissing()
    {
        var html = """
            <html><head>
            <meta name="twitter:title" content="Twitter Title" />
            <meta name="twitter:description" content="Twitter Desc" />
            </head></html>
            """;
        var controller = Build(HtmlResponse(html));

        var result = await controller.GetPreview("https://example.com/twitter");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().Be("Twitter Title");
        dto.Description.Should().Be("Twitter Desc");
    }

    [Fact]
    public async Task GetPreview_ParsesDescriptionMetaTag_WhenOgAndTwitterMissing()
    {
        var html = """
            <html><head>
            <meta name="description" content="Meta Description" />
            </head></html>
            """;
        var controller = Build(HtmlResponse(html));

        var result = await controller.GetPreview("https://example.com/meta-desc");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Description.Should().Be("Meta Description");
    }

    [Fact]
    public async Task GetPreview_CachesResult_AfterFirstFetch()
    {
        var html       = """<html><head><title>Cached Page</title></head></html>""";
        var controller = Build(HtmlResponse(html));

        // First call should fetch
        var result1 = await controller.GetPreview("https://example.com/cacheable");
        result1.Should().BeOfType<OkObjectResult>();

        // Second call should use the cache (even without a new Build with response)
        var controller2 = Build(); // No response configured
        var result2 = await controller2.GetPreview("https://example.com/cacheable");

        // Cached result should be returned even without a response configured
        result2.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPreview_ReturnsEmptyPreview_WhenHtmlHasNoMeta()
    {
        var html = """<html><head></head><body>No meta tags here</body></html>""";
        var controller = Build(HtmlResponse(html));

        var result = await controller.GetPreview("https://example.com/no-meta");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (OgPreviewDto)((OkObjectResult)result).Value!;
        dto.Title.Should().BeNull();
        dto.Description.Should().BeNull();
    }
}
