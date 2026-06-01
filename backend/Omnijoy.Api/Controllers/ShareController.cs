using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Api.Controllers;

/// <summary>
/// Server-side rendered HTML shells for shareable links.
///
/// The Vue SPA cannot inject Open Graph / Twitter Card meta tags into the
/// initial HTML response — bots / crawlers (Facebook, Twitter, Slack,
/// Discord, etc.) never execute JavaScript, so they would only see the
/// generic <c>index.html</c> head.
///
/// This controller bypasses the SPA fallback for the <c>/share/...</c>
/// route family: it reads the matching entity from the database, takes the
/// built <c>wwwroot/index.html</c> shell, and rewrites the &lt;head&gt; with
/// the correct OG / Twitter meta tags before returning it. The SPA picks up
/// the route as normal for human visitors.
/// </summary>
[ApiController]
[Route("share")]
public class ShareController : ControllerBase
{
    private readonly OmnijoyDbContext _db;
    private readonly IWebHostEnvironment _env;

    private const string DefaultSiteName = "Omnijoy";
    private const string DefaultDescription = "Omnijoy — a social platform without ads or forced content.";

    public ShareController(OmnijoyDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── GET /share/posts/{id} ─────────────────────────────────────────────────

    [HttpGet("posts/{id:guid}")]
    public async Task<IActionResult> SharePost(Guid id)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null);

        var canonical = BuildCanonical($"/share/posts/{id}");

        if (post is null)
        {
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"Post not found — {DefaultSiteName}",
                Description: "This post does not exist or has been deleted.",
                ImageUrl: null,
                Canonical: canonical,
                OgType: "website",
                Author: null,
                IsPublic: false,
                NotPublicMessage: "This post does not exist or has been deleted.")));
        }

        if (post.Privacy != PrivacyLevel.Everyone)
        {
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"Private post — {DefaultSiteName}",
                Description: "This post is not public.",
                ImageUrl: null,
                Canonical: canonical,
                OgType: "website",
                Author: post.Author.DisplayName,
                IsPublic: false,
                NotPublicMessage: "The author has restricted who can see this post.")));
        }

        // Preview image priority: first media image → post link image → author avatar.
        var image = post.Media
                        .OrderBy(m => m.Order)
                        .FirstOrDefault(m => m.MediaType == MediaType.Image)?.Url
                    ?? post.LinkImageUrl
                    ?? post.Author.AvatarUrl;

        var title = string.IsNullOrWhiteSpace(post.Content)
            ? $"Post by {post.Author.DisplayName}"
            : Truncate(post.Content, 90);

        var description = Truncate(
            string.IsNullOrWhiteSpace(post.Content)
                ? $"{post.Author.DisplayName} shared a post on Omnijoy."
                : post.Content,
            300);

        return HtmlContent(BuildShell(new MetaTags(
            Title: title,
            Description: description,
            ImageUrl: AbsoluteUrl(image),
            Canonical: canonical,
            OgType: "article",
            Author: post.Author.DisplayName,
            IsPublic: true,
            NotPublicMessage: null)));
    }

    // ── GET /share/users/{id} ─────────────────────────────────────────────────

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> ShareUser(Guid id)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.PrivacySettings)
            .FirstOrDefaultAsync(u => u.Id == id);

        var canonical = BuildCanonical($"/share/users/{id}");

        if (user is null)
        {
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"User not found — {DefaultSiteName}",
                Description: "This user profile does not exist.",
                ImageUrl: null,
                Canonical: canonical,
                OgType: "website",
                Author: null,
                IsPublic: false,
                NotPublicMessage: "This user profile does not exist.")));
        }

        var who = user.PrivacySettings?.WhoCanSeeProfile ?? PrivacyLevel.Everyone;
        if (who != PrivacyLevel.Everyone)
        {
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"Private profile — {DefaultSiteName}",
                Description: $"{user.DisplayName} has restricted who can see their profile.",
                ImageUrl: null,
                Canonical: canonical,
                OgType: "profile",
                Author: user.DisplayName,
                IsPublic: false,
                NotPublicMessage: $"{user.DisplayName} has restricted who can see their profile.")));
        }

        var title = $"{user.DisplayName} on Omnijoy";
        var description = string.IsNullOrWhiteSpace(user.Bio)
            ? $"View {user.DisplayName}'s profile on Omnijoy."
            : Truncate(user.Bio, 300);

        return HtmlContent(BuildShell(new MetaTags(
            Title: title,
            Description: description,
            ImageUrl: AbsoluteUrl(user.AvatarUrl ?? user.CoverUrl),
            Canonical: canonical,
            OgType: "profile",
            Author: user.DisplayName,
            IsPublic: true,
            NotPublicMessage: null)));
    }

    // ── GET /share/events/{id} ────────────────────────────────────────────────

    [HttpGet("events/{id:guid}")]
    public async Task<IActionResult> ShareEvent(Guid id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .FirstOrDefaultAsync(e => e.Id == id);

        var canonical = BuildCanonical($"/share/events/{id}");

        if (ev is null)
        {
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"Event not found — {DefaultSiteName}",
                Description: "This event does not exist or has been deleted.",
                ImageUrl: null,
                Canonical: canonical,
                OgType: "website",
                Author: null,
                IsPublic: false,
                NotPublicMessage: "This event does not exist or has been deleted.")));
        }

        if (ev.Privacy != PrivacyLevel.Everyone)
        {
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"Private event — {DefaultSiteName}",
                Description: "This event is not public.",
                ImageUrl: null,
                Canonical: canonical,
                OgType: "event",
                Author: ev.CreatorUser.DisplayName,
                IsPublic: false,
                NotPublicMessage: "The organiser has restricted who can see this event.")));
        }

        var when = ev.StartAt.ToString("dddd, MMMM d yyyy 'at' HH:mm 'UTC'");
        var descriptionParts = new List<string> { when };
        if (!string.IsNullOrWhiteSpace(ev.Location)) descriptionParts.Add(ev.Location);
        if (!string.IsNullOrWhiteSpace(ev.Description)) descriptionParts.Add(ev.Description!);
        var description = Truncate(string.Join(" · ", descriptionParts), 300);

        return HtmlContent(BuildShell(new MetaTags(
            Title: ev.Title,
            Description: description,
            ImageUrl: AbsoluteUrl(ev.CoverImageUrl),
            Canonical: canonical,
            OgType: "event",
            Author: ev.CreatorUser.DisplayName,
            IsPublic: true,
            NotPublicMessage: null)));
    }

    // ── GET /invite/{token} ──────────────────────────────────────────────────
    // Serves the SPA shell enriched with OG / Twitter Card meta tags so that
    // social-media bots (Facebook, Twitter, Slack, Discord …) see a rich
    // preview when a friend invite link is shared.  Human visitors get the
    // same response — the SPA boots normally and renders InviteAcceptView.

    private const string InviteTagline =
        "the new social platform - no ads - just you, your friends and the events you go to";

    [HttpGet("/invite/{token}")]
    public async Task<IActionResult> ShareInvite(string token)
    {
        var invite = await _db.FriendInvites
            .AsNoTracking()
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token);

        var canonical = BuildCanonical($"/invite/{token}");

        if (invite is null ||
            invite.Status == FriendInviteStatus.Revoked ||
            invite.ExpiresAt < DateTime.UtcNow)
        {
            // Invalid / expired invite — still serve a branded OG shell so
            // the share card at least shows the site logo and tagline.
            return HtmlContent(BuildShell(new MetaTags(
                Title: $"Join OmniJoy — {InviteTagline}",
                Description: $"Join OmniJoy — {InviteTagline}",
                ImageUrl: AbsoluteUrl("/logo.png"),
                Canonical: canonical,
                OgType: "website",
                Author: null,
                IsPublic: true,
                NotPublicMessage: null)));
        }

        var title       = $"Join {invite.Inviter.DisplayName} on OmniJoy - {InviteTagline}";
        var description = title;
        // Prefer the inviter's avatar; fall back to the site logo.
        var image = invite.Inviter.AvatarUrl ?? "/logo.png";

        return HtmlContent(BuildShell(new MetaTags(
            Title: title,
            Description: description,
            ImageUrl: AbsoluteUrl(image),
            Canonical: canonical,
            OgType: "website",
            Author: invite.Inviter.DisplayName,
            IsPublic: true,
            NotPublicMessage: null)));
    }

    // ── Shell construction ────────────────────────────────────────────────────

    /// <summary>Bundle of values needed to render meta tags + page body.</summary>
    private sealed record MetaTags(
        string Title,
        string Description,
        string? ImageUrl,
        string Canonical,
        string OgType,
        string? Author,
        bool IsPublic,
        string? NotPublicMessage);

    /// <summary>
    /// Builds the response HTML. When the SPA's built <c>index.html</c> exists
    /// we inject our meta tags into its &lt;head&gt; so the Vue app still
    /// boots and renders the SharePostView / etc. for human visitors. When
    /// it doesn't (e.g. backend running before frontend has been built), we
    /// fall back to a static crawler-only shell.
    /// </summary>
    private string BuildShell(MetaTags meta)
    {
        var indexPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "index.html");
        if (System.IO.File.Exists(indexPath))
        {
            var template = System.IO.File.ReadAllText(indexPath);
            return InjectMetaTags(template, meta);
        }
        return BuildStaticShell(meta);
    }

    private static string InjectMetaTags(string template, MetaTags meta)
    {
        var injected = BuildMetaTagBlock(meta);

        // Replace the existing <title> if present
        var titleEncoded = WebUtility.HtmlEncode(meta.Title);
        template = Regex.Replace(
            template,
            "<title>[^<]*</title>",
            $"<title>{titleEncoded}</title>",
            RegexOptions.IgnoreCase);

        // Insert OG block right before </head>
        var idx = template.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return template + injected;
        return template.Insert(idx, injected);
    }

    private static string BuildMetaTagBlock(MetaTags meta)
    {
        var sb = new StringBuilder();
        var encDescription = WebUtility.HtmlEncode(meta.Description);
        var encCanonical = WebUtility.HtmlEncode(meta.Canonical);
        var encTitle = WebUtility.HtmlEncode(meta.Title);
        var encImage = meta.ImageUrl is null ? null : WebUtility.HtmlEncode(meta.ImageUrl);

        sb.Append("\n    <!-- Open Graph meta tags (server-rendered for crawlers) -->\n");
        sb.Append($"    <meta name=\"description\" content=\"{encDescription}\"/>\n");
        sb.Append($"    <link rel=\"canonical\" href=\"{encCanonical}\"/>\n");

        sb.Append($"    <meta property=\"og:type\" content=\"{WebUtility.HtmlEncode(meta.OgType)}\"/>\n");
        sb.Append($"    <meta property=\"og:site_name\" content=\"{DefaultSiteName}\"/>\n");
        sb.Append($"    <meta property=\"og:title\" content=\"{encTitle}\"/>\n");
        sb.Append($"    <meta property=\"og:description\" content=\"{encDescription}\"/>\n");
        sb.Append($"    <meta property=\"og:url\" content=\"{encCanonical}\"/>\n");
        if (encImage is not null)
            sb.Append($"    <meta property=\"og:image\" content=\"{encImage}\"/>\n");

        sb.Append("    <meta name=\"twitter:card\" content=\"summary_large_image\"/>\n");
        sb.Append($"    <meta name=\"twitter:title\" content=\"{encTitle}\"/>\n");
        sb.Append($"    <meta name=\"twitter:description\" content=\"{encDescription}\"/>\n");
        if (encImage is not null)
            sb.Append($"    <meta name=\"twitter:image\" content=\"{encImage}\"/>\n");
        if (!string.IsNullOrWhiteSpace(meta.Author))
            sb.Append($"    <meta name=\"author\" content=\"{WebUtility.HtmlEncode(meta.Author)}\"/>\n");

        return sb.ToString();
    }

    private static string BuildStaticShell(MetaTags meta)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"en\"><head>");
        sb.Append("<meta charset=\"UTF-8\"/>");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/>");
        sb.Append($"<title>{WebUtility.HtmlEncode(meta.Title)}</title>");
        sb.Append(BuildMetaTagBlock(meta));
        sb.Append("</head><body style=\"font-family:sans-serif;text-align:center;padding:3rem\">");
        if (meta.IsPublic)
        {
            sb.Append($"<h1>{WebUtility.HtmlEncode(meta.Title)}</h1>");
            sb.Append($"<p>{WebUtility.HtmlEncode(meta.Description)}</p>");
        }
        else
        {
            sb.Append($"<h1>{WebUtility.HtmlEncode(meta.Title)}</h1>");
            sb.Append($"<p>{WebUtility.HtmlEncode(meta.NotPublicMessage ?? meta.Description)}</p>");
        }
        sb.Append($"<p><a href=\"/\">Back to {DefaultSiteName}</a></p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ContentResult HtmlContent(string html)
    {
        // Crawler caches: short Cache-Control so updates propagate but repeat
        // hits during a single share-burst are cheap.
        Response.Headers["Cache-Control"] = "public, max-age=300";
        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    private string BuildCanonical(string path)
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}{path}";
    }

    /// <summary>
    /// Turns a possibly-relative storage path ("/uploads/foo.jpg") into an
    /// absolute URL so crawlers can fetch it.
    /// </summary>
    private string? AbsoluteUrl(string? maybeRelative)
    {
        if (string.IsNullOrWhiteSpace(maybeRelative)) return null;
        if (Uri.TryCreate(maybeRelative, UriKind.Absolute, out _)) return maybeRelative;

        var request = HttpContext.Request;
        var path = maybeRelative.StartsWith('/') ? maybeRelative : "/" + maybeRelative;
        return $"{request.Scheme}://{request.Host}{path}";
    }

    private static string Truncate(string value, int max)
    {
        value = value.Trim();
        if (value.Length <= max) return value;
        return value[..(max - 1)] + "…";
    }
}
