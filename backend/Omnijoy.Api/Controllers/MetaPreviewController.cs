using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Omnijoy.Core.DTOs.Chat;
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
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public MetaPreviewController(IHttpClientFactory http, IMemoryCache cache)
    {
        _http  = http;
        _cache = cache;
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

        // Block SSRF — private / loopback addresses
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1" or "::1" ||
            host.StartsWith("192.168.") ||
            host.StartsWith("10.")      ||
            host.StartsWith("172.16.")  ||
            host.StartsWith("172.17.")  ||
            host.StartsWith("172.18.")  ||
            host.StartsWith("172.19.")  ||
            host.StartsWith("172.2")    ||
            host.StartsWith("172.30.")  ||
            host.StartsWith("172.31."))
            return BadRequest(new { error = "URL points to a private network address." });

        var cacheKey = $"ogpreview:{uri.AbsoluteUri}";
        if (_cache.TryGetValue(cacheKey, out OgPreviewDto? cached))
            return Ok(cached);

        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OmnijoyBot/1.0 (+https://omnijoy.local)");
            client.Timeout = TimeSpan.FromSeconds(8);

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

    // ── Helpers ───────────────────────────────────────────────────────────────

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
