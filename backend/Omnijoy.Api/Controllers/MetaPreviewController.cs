using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Omnijoy.Core.DTOs.Chat;
using Omnijoy.Core.Interfaces;
using System.Text.RegularExpressions;

namespace Omnijoy.Api.Controllers;

/// <summary>
/// Fetches Open Graph / Twitter Card meta-tags from an external URL and
/// returns a preview card payload.  Results are cached for 30 minutes.
/// </summary>
[ApiController]
[Route("api/meta-preview")]
public class MetaPreviewController : ControllerBase
{
    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly IHostResolver _resolver;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public MetaPreviewController(IHttpClientFactory http, IMemoryCache cache, IHostResolver resolver)
    {
        _http     = http;
        _cache    = cache;
        _resolver = resolver;
    }

    // ── GET /api/meta-preview?url=... ─────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetPreview([FromQuery] string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = "url parameter is required." });

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { error = "url must be an absolute HTTP or HTTPS URL." });

        // ── SSRF protection ─────────────────────────────────────────────────
        var host = uri.Host.ToLowerInvariant();

        // Fast path: literal "localhost" hostname
        if (host == "localhost")
            return BadRequest(new { error = "URL points to a private network address." });

        // If the host is already an IP literal, check it directly.
        // Otherwise resolve via DNS and check all returned addresses.
        if (IPAddress.TryParse(host, out var literalIp))
        {
            if (IsPrivateOrReservedAddress(literalIp))
                return BadRequest(new { error = "URL points to a private network address." });
        }
        else
        {
            IPAddress[] addresses;
            try
            {
                addresses = await _resolver.GetHostAddressesAsync(host);
            }
            catch
            {
                return BadRequest(new { error = "Could not resolve host." });
            }

            if (addresses.Any(IsPrivateOrReservedAddress))
                return BadRequest(new { error = "URL resolves to a private network address." });
        }

        var cacheKey = $"ogpreview:{uri.AbsoluteUri}";
        if (_cache.TryGetValue(cacheKey, out OgPreviewDto? cached))
            return Ok(cached);

        try
        {
            var client = _http.CreateClient("MetaPreview");

            using var response = await client.GetAsync(
                uri.AbsoluteUri, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return Ok(EmptyPreview(url));

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return Ok(EmptyPreview(url));

            // Read only the first 128 KB to avoid downloading huge pages
            var buffer = new byte[128 * 1024];
            await using var stream = await response.Content.ReadAsStreamAsync();
            var bytesRead = await stream.ReadAsync(buffer);
            var html = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

            var preview = ParseOgTags(html, url);
            _cache.Set(cacheKey, preview, CacheDuration);
            return Ok(preview);
        }
        catch
        {
            // Network errors, timeouts, etc. — return an empty preview rather than 5xx
            return Ok(EmptyPreview(url));
        }
    }

    // ── SSRF helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="addr"/> falls within any private,
    /// loopback, link-local, or reserved IP range.
    /// </summary>
    private static bool IsPrivateOrReservedAddress(IPAddress addr)
    {
        // Unwrap IPv4-mapped IPv6 (e.g. ::ffff:127.0.0.1)
        if (addr.IsIPv4MappedToIPv6)
            addr = addr.MapToIPv4();

        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // IPv4 checks
            return
                InRange(addr, IPAddress.Parse("127.0.0.0"),   8)  || // loopback
                InRange(addr, IPAddress.Parse("169.254.0.0"), 16) || // link-local / APIPA / AWS metadata
                InRange(addr, IPAddress.Parse("10.0.0.0"),    8)  || // RFC 1918
                InRange(addr, IPAddress.Parse("172.16.0.0"),  12) || // RFC 1918
                InRange(addr, IPAddress.Parse("192.168.0.0"), 16) || // RFC 1918
                InRange(addr, IPAddress.Parse("100.64.0.0"),  10);   // RFC 6598 CGN / Tailscale
        }

        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // IPv6 checks
            if (addr.Equals(IPAddress.IPv6Loopback))           // ::1
                return true;
            if (InRange(addr, IPAddress.Parse("fe80::"), 10))  // link-local
                return true;
            if (InRange(addr, IPAddress.Parse("fc00::"), 7))   // ULA (fc00::/7 covers fc00:: and fd00::)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the high <paramref name="prefixBits"/> of
    /// <paramref name="addr"/> match those of <paramref name="network"/>.
    /// </summary>
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

    // ── Parse helpers ─────────────────────────────────────────────────────────

    private static OgPreviewDto EmptyPreview(string url)
        => new(url, null, null, null, null);

    private static OgPreviewDto ParseOgTags(string html, string url)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Patterns: <meta property="og:xxx" content="yyy" /> (attribute order varies)
        var regexes = new[]
        {
            new Regex(
                @"<meta\s[^>]*property=[""'](?<prop>[^""']+)[""'][^>]*content=[""'](?<val>[^""']*)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            new Regex(
                @"<meta\s[^>]*content=[""'](?<val>[^""']*)[""'][^>]*property=[""'](?<prop>[^""']+)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            new Regex(
                @"<meta\s[^>]*name=[""'](?<prop>[^""']+)[""'][^>]*content=[""'](?<val>[^""']*)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            new Regex(
                @"<meta\s[^>]*content=[""'](?<val>[^""']*)[""'][^>]*name=[""'](?<prop>[^""']+)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
        };

        foreach (var rx in regexes)
        {
            foreach (Match m in rx.Matches(html))
            {
                var prop = m.Groups["prop"].Value;
                var val  = m.Groups["val"].Value;
                if (!string.IsNullOrEmpty(prop) && !tags.ContainsKey(prop))
                    tags[prop] = System.Net.WebUtility.HtmlDecode(val);
            }
        }

        // og:title → twitter:title → <title> element fallback
        string? title = tags.GetValueOrDefault("og:title")
            ?? tags.GetValueOrDefault("twitter:title");

        if (title is null)
        {
            var titleMatch = Regex.Match(
                html, @"<title[^>]*>(?<t>[^<]+)</title>", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
                title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups["t"].Value.Trim());
        }

        var description = tags.GetValueOrDefault("og:description")
            ?? tags.GetValueOrDefault("twitter:description")
            ?? tags.GetValueOrDefault("description");

        var image = tags.GetValueOrDefault("og:image")
            ?? tags.GetValueOrDefault("twitter:image");

        var siteName = tags.GetValueOrDefault("og:site_name");

        return new OgPreviewDto(url, title, description, image, siteName);
    }
}
