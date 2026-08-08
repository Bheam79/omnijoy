using System.Net;
using System.Net.Sockets;

namespace Omnijoy.Api.Services;

/// <summary>
/// Shared private/reserved-IP classification. Used by both
/// <see cref="Controllers.MetaPreviewController"/>'s pre-flight check and
/// <see cref="SsrfSafeConnectCallback"/>'s connect-time enforcement, so the
/// two can't silently diverge (e.g. a new reserved range added to one and
/// forgotten in the other).
/// </summary>
public static class SsrfGuard
{
    public static bool IsPrivateOrReservedAddress(IPAddress addr)
    {
        // Unwrap IPv4-mapped IPv6 (e.g. ::ffff:127.0.0.1)
        if (addr.IsIPv4MappedToIPv6)
            addr = addr.MapToIPv4();

        if (addr.AddressFamily == AddressFamily.InterNetwork)
        {
            return
                InRange(addr, IPAddress.Parse("127.0.0.0"),   8)  || // loopback
                InRange(addr, IPAddress.Parse("169.254.0.0"), 16) || // link-local / APIPA / AWS metadata
                InRange(addr, IPAddress.Parse("10.0.0.0"),    8)  || // RFC 1918
                InRange(addr, IPAddress.Parse("172.16.0.0"),  12) || // RFC 1918
                InRange(addr, IPAddress.Parse("192.168.0.0"), 16) || // RFC 1918
                InRange(addr, IPAddress.Parse("100.64.0.0"),  10);   // RFC 6598 CGN / Tailscale
        }

        if (addr.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (addr.Equals(IPAddress.IPv6Loopback))           // ::1
                return true;
            if (InRange(addr, IPAddress.Parse("fe80::"), 10))  // link-local
                return true;
            if (InRange(addr, IPAddress.Parse("fc00::"), 7))   // ULA (fc00::/7 covers fc00:: and fd00::)
                return true;
        }

        return false;
    }

    private static bool InRange(IPAddress addr, IPAddress network, int prefixBits)
    {
        var a = addr.GetAddressBytes();
        var n = network.GetAddressBytes();
        if (a.Length != n.Length) return false;
        int fullBytes = prefixBits / 8;
        int rem       = prefixBits % 8;
        for (int i = 0; i < fullBytes; i++)
            if (a[i] != n[i]) return false;
        if (rem > 0)
        {
            byte mask = (byte)(0xFF << (8 - rem));
            if ((a[fullBytes] & mask) != (n[fullBytes] & mask)) return false;
        }
        return true;
    }
}
