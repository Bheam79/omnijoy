using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Services;

/// <summary>
/// Production implementation of <see cref="IHostResolver"/> that delegates
/// to <see cref="System.Net.Dns.GetHostAddressesAsync(string, CancellationToken)"/>.
/// </summary>
public sealed class DnsHostResolver : IHostResolver
{
    public Task<System.Net.IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default)
        => System.Net.Dns.GetHostAddressesAsync(host, ct);
}
