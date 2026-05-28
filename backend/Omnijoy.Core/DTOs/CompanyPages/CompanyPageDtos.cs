namespace Omnijoy.Core.DTOs.CompanyPages;

// ── Sub-records ────────────────────────────────────────────────────────────────

public record CompanyPageCreatorDto(Guid Id, string DisplayName, string? AvatarUrl);

public record CompanyPageAdminDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role
);

// ── Company page response ─────────────────────────────────────────────────────

public record CompanyPageDto(
    Guid Id,
    string Name,
    string? Description,
    string? LogoUrl,
    string? CoverUrl,
    CompanyPageCreatorDto CreatedBy,
    int FollowerCount,
    bool IsFollowing,
    /// <summary>Current user's role; null if not an admin.</summary>
    string? MyRole,
    DateTime CreatedAt,
    /// <summary>Vanity URL slug — null when unset.</summary>
    string? UrlSlug = null
);

// ── Create / Update requests ──────────────────────────────────────────────────

public record CreateCompanyPageRequest(
    string Name,
    string? Description
);

public record UpdateCompanyPageRequest(
    string? Name,
    string? Description
);

// ── Admin management ──────────────────────────────────────────────────────────

public record AddAdminRequest(
    Guid UserId,
    /// <summary>"Admin" | "Editor"</summary>
    string Role
);

// ── Image upload helpers ──────────────────────────────────────────────────────

public record PageImageUploadItem(Stream Content, string FileName, string ContentType, string Kind);

// ── Lists ─────────────────────────────────────────────────────────────────────

public record CompanyPagesPageResult(
    CompanyPageDto[] Items,
    int Page,
    int PageSize,
    bool HasMore
);

public record AdminsResult(CompanyPageAdminDto[] Admins);
