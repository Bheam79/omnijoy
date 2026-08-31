using System.Text.Json.Serialization;
using Omnijoy.Core.DTOs;

namespace Omnijoy.Core.DTOs.Posts;

// ── Embedded sub-records ───────────────────────────────────────────────────────

public record PostAuthorDto(Guid Id, string DisplayName, string? AvatarUrl);

/// <summary>
/// Lightweight company-page snapshot embedded in a post when the post was
/// published on behalf of a company page. Frontend displays this as the
/// post author instead of the user when present.
/// </summary>
public record PostCompanyPageDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    /// <summary>Vanity URL slug; null when unset.</summary>
    string? UrlSlug
);

public record PostMediaItemDto(
    Guid Id,
    string MediaType,
    string Url,
    string? ThumbnailUrl,
    int Order
);

/// <summary>
/// Open Graph / link preview embedded in a post (the URL the author pasted +
/// title / description / image fetched at compose time).
/// </summary>
public record PostLinkPreviewDto(
    string Url,
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName
);

// ── Post response ─────────────────────────────────────────────────────────────

public record PostDto(
    Guid Id,
    PostAuthorDto Author,
    Guid? CompanyPageId,
    string Content,
    string? BackgroundImageUrl,
    string PostType,
    string Privacy,
    PostMediaItemDto[] Media,
    PostLinkPreviewDto? LinkPreview,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>
    /// When set, the post was published on behalf of this company page —
    /// frontend should display the page (name + logo + slug link) as the
    /// post author rather than the underlying user. Null for regular
    /// personal posts.
    /// </summary>
    PostCompanyPageDto? CompanyPage = null,
    /// <summary>Whether the authenticated requester has privately saved this post.</summary>
    bool IsSavedByMe = false,
    /// <summary>
    /// Total private saves. Populated only when the authenticated requester is
    /// the post author and omitted from JSON for every other viewer.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? SavesCount = null,
    /// <summary>Resolved mentions persisted for this content.</summary>
    MentionDto[]? Mentions = null
);

// ── Create / Update requests ──────────────────────────────────────────────────

/// <summary>
/// JSON body for POST /api/posts (media files are sent as form data separately).
/// </summary>
public record CreatePostRequest(
    string Content,
    /// <summary>"Text" | "Image" | "Video" | "TextOnBackground"</summary>
    string PostType,
    /// <summary>"Everyone" | "Friends" | "OnlyMe"</summary>
    string Privacy,
    string? BackgroundImageUrl,
    /// <summary>Optional: post on behalf of a company page the user administers.</summary>
    Guid? CompanyPageId = null,
    /// <summary>Optional embedded link preview (URL the user pasted into the composer).</summary>
    string? LinkUrl = null,
    string? LinkTitle = null,
    string? LinkDescription = null,
    string? LinkImageUrl = null,
    string? LinkSiteName = null
);

public record UpdatePostRequest(
    string? Content,
    string? Privacy
);

// ── Shared-post feed item ─────────────────────────────────────────────────────

/// <summary>
/// Represents a shared post as it appears in the feed.
/// </summary>
public record SharedPostFeedItemDto(
    Guid Id,
    PostAuthorDto Sharer,
    string? Message,
    /// <summary>"OwnWall" | "FriendWall" | "CompanyPage"</summary>
    string TargetType,
    Guid? TargetId,
    PostDto OriginalPost,
    DateTime CreatedAt
);

// ── Discriminated union feed item ─────────────────────────────────────────────

/// <summary>
/// A single item in the feed, which is either a regular post or a shared post.
/// <c>ItemType</c> is "Post" or "SharedPost".
/// </summary>
public record FeedItemDto(
    /// <summary>"Post" | "SharedPost"</summary>
    string ItemType,
    PostDto? Post,
    SharedPostFeedItemDto? SharedPost
);

// ── Share request ─────────────────────────────────────────────────────────────

/// <summary>
/// JSON body for POST /api/posts/{id}/share.
/// </summary>
public record CreateShareRequest(
    /// <summary>"OwnWall" | "FriendWall" | "CompanyPage"</summary>
    string TargetType,
    /// <summary>Friend user ID (FriendWall) or company page ID (CompanyPage). Null for OwnWall.</summary>
    Guid? TargetId,
    string? Message
);

// ── Media upload helper (used by controller → service) ────────────────────────

/// <summary>
/// Wrapper around an uploaded file stream, so Core's interface
/// remains free of ASP.NET Core types.
/// </summary>
public record MediaUploadItem(Stream Content, string FileName, string ContentType);
