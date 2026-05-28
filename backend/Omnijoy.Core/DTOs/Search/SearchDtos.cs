namespace Omnijoy.Core.DTOs.Search;

// ── Per-entity result shapes ───────────────────────────────────────────────────

public record UserSearchResult(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string? Bio
);

public record PostSearchResult(
    Guid Id,
    Guid AuthorId,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content,
    string PostType,
    string Privacy,
    DateTime CreatedAt
);

public record EventSearchResult(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    string? CoverImageUrl,
    Guid CreatorId,
    string CreatorDisplayName,
    Guid? CompanyPageId
);

public record CompanySearchResult(
    Guid Id,
    string Name,
    string? Description,
    string? LogoUrl,
    int FollowerCount
);

// ── Unified response ──────────────────────────────────────────────────────────

/// <summary>
/// Search results returned by GET /api/search.
/// When <c>Type</c> is "all", all four arrays are populated (up to <c>PageSize</c> each).
/// When <c>Type</c> is a specific category, only that array contains results; the rest are empty.
/// </summary>
public record SearchResponse(
    string Query,
    /// <summary>"all" | "users" | "posts" | "events" | "companies"</summary>
    string Type,
    UserSearchResult[]    Users,
    PostSearchResult[]    Posts,
    EventSearchResult[]   Events,
    CompanySearchResult[] Companies,
    int Page,
    int PageSize,
    bool HasMore
);
