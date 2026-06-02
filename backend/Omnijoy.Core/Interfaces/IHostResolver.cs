namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Abstracts DNS resolution so MetaPreviewController can be unit-tested
/// without real network calls.
/// </summary>
public interface IHostResolver
{
    Task<System.Net.IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default);
}
