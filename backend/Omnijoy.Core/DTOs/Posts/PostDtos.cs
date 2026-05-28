namespace Omnijoy.Core.DTOs.Posts;

// ── Embedded sub-records ───────────────────────────────────────────────────────

public record PostAuthorDto(Guid Id, string DisplayName, string? AvatarUrl);

public record PostMediaItemDto(
    Guid Id,
    string MediaType,
    string Url,
    string? ThumbnailUrl,
    int Order
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
    DateTime CreatedAt,
    DateTime UpdatedAt
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
    Guid? CompanyPageId = null
);

public record UpdatePostRequest(
    string? Content,
    string? Privacy
);

// ── Feed pagination ───────────────────────────────────────────────────────────

public record FeedPageResult(
    PostDto[] Items,
    int Page,
    int PageSize,
    bool HasMore
);

// ── Media upload helper (used by controller → service) ────────────────────────

/// <summary>
/// Wrapper around an uploaded file stream, so Core's interface
/// remains free of ASP.NET Core types.
/// </summary>
public record MediaUploadItem(Stream Content, string FileName, string ContentType);
