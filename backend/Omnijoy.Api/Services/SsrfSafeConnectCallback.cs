using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Services;

/// <summary>
/// <see cref="SocketsHttpHandler.ConnectCallback"/> for the "MetaPreview"
/// named <see cref="HttpClient"/>.
///
/// <see cref="Controllers.MetaPreviewController"/> validates the target host
/// via <see cref="IHostResolver"/> before making the request, but that DNS
/// lookup is separate from the one the HTTP stack performs itself when it
/// actually opens the socket. An attacker who controls DNS for their domain
/// can return a public IP for the pre-flight lookup (passing the check) and
/// a private/internal IP a moment later for the real connection — a classic
/// DNS-rebinding TOCTOU bypass of the pre-flight check.
///
/// This callback re-resolves and re-validates the host at the exact moment
/// the socket is opened and connects directly to the validated address, so
/// there is no second, unchecked DNS lookup left for an attacker to race.
/// </summary>
public sealed class SsrfSafeConnectCallback
{
    private readonly IHostResolver _resolver;

    public SsrfSafeConnectCallback(IHostResolver resolver) => _resolver = resolver;

    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await _resolver.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(SsrfGuard.IsPrivateOrReservedAddress))
        {
            throw new InvalidOperationException(
                $"Refusing to connect to '{host}': resolves to zero or private/reserved address(es).");
        }

        var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(addresses[0], port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
