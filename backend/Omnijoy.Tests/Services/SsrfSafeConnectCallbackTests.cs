using System.Net;
using System.Net.Http;
using FluentAssertions;
using Moq;
using Omnijoy.Api.Services;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Verifies the connect-time SSRF enforcement closes the DNS-rebinding TOCTOU
/// gap: even if a hostname resolved to a public address during
/// MetaPreviewController's pre-flight check, ConnectAsync re-resolves and
/// re-validates at the moment the socket is opened and refuses to connect if
/// that resolution turns out to be private/reserved.
///
/// <see cref="System.Net.Http.SocketsHttpConnectionContext"/> has no public
/// constructor, so these tests drive the callback the same way production
/// code does: through a real <see cref="SocketsHttpHandler"/> with
/// <c>ConnectCallback</c> set. Setting <c>ConnectCallback</c> makes the
/// handler delegate connection establishment entirely to our callback (it
/// performs no DNS/socket work of its own first), so the private-address
/// cases below never touch the network — they throw before any I/O.
/// </summary>
public class SsrfSafeConnectCallbackTests
{
    private static HttpClient BuildClient(IHostResolver resolver)
    {
        var callback = new SsrfSafeConnectCallback(resolver);
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = callback.ConnectAsync,
            ConnectTimeout = TimeSpan.FromSeconds(2),
        };
        return new HttpClient(handler);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    public async Task Request_ThrowsBeforeAnyNetworkIO_WhenResolvedAddressIsPrivate(string ip)
    {
        var resolver = new Mock<IHostResolver>();
        resolver
            .Setup(r => r.GetHostAddressesAsync("evil-rebind.example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([IPAddress.Parse(ip)]);

        using var client = BuildClient(resolver.Object);

        var act = async () => await client.GetAsync("http://evil-rebind.example.com/");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Request_Throws_WhenResolverReturnsNoAddresses()
    {
        var resolver = new Mock<IHostResolver>();
        resolver
            .Setup(r => r.GetHostAddressesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var client = BuildClient(resolver.Object);

        var act = async () => await client.GetAsync("http://nowhere.example.com/");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Request_DoesNotCallResolver_WhenHostIsPrivateIpLiteral()
    {
        var resolver = new Mock<IHostResolver>();
        using var client = BuildClient(resolver.Object);

        var act = async () => await client.GetAsync("http://127.0.0.1/");

        await act.Should().ThrowAsync<HttpRequestException>();
        resolver.Verify(
            r => r.GetHostAddressesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Request_RejectsHost_WhenAnyResolvedAddressIsPrivate_EvenAmongPublicOnes()
    {
        // Mirrors the real rebinding shape: one address in the DNS response
        // is public (would pass a naive "first address" check), the other is
        // private. The whole host must be refused, not just the private
        // address skipped, since a later resolution could return only the
        // private one.
        var resolver = new Mock<IHostResolver>();
        resolver
            .Setup(r => r.GetHostAddressesAsync("mixed.example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([IPAddress.Parse("93.184.216.34"), IPAddress.Parse("10.0.0.1")]);

        using var client = BuildClient(resolver.Object);

        var act = async () => await client.GetAsync("http://mixed.example.com/");

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
