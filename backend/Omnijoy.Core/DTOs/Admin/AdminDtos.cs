namespace Omnijoy.Core.DTOs.Admin;

// ── Requests ─────────────────────────────────────────────────────────────────

/// <summary>
/// Body for PATCH /api/admin/users/{id}/role.
/// Role must be one of: "User", "Moderator", "Admin".
/// </summary>
public record ChangeUserRoleRequest(string Role);

/// <summary>
/// Body for POST /api/admin/users/{id}/ban and /api/admin/users/{id}/unban.
/// Notes are optional and copied into the matching <c>ModerationLogs</c> row.
/// </summary>
public record BanUserRequest(string? Notes);

// ── Responses ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight user summary returned by admin user-management endpoints.
/// </summary>
public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsBanned,
    DateTime? BannedAt,
    DateTime CreatedAt
);

/// <summary>
/// Single row from the moderation audit log.
/// </summary>
public record ModerationLogDto(
    Guid Id,
    Guid ActorId,
    string ActorDisplayName,
    /// <summary>
    /// The action name (e.g. "BanUser", "DismissReport"). See
    /// <c>Omnijoy.Core.Models.Enums.ModerationAction</c> for the full list.
    /// </summary>
    string Action,
    string TargetType,
    string TargetId,
    string? Notes,
    DateTime CreatedAt
);

/// <summary>
/// Paginated result for GET /api/admin/audit-log.
/// </summary>
public record ModerationLogListResult(
    ModerationLogDto[] Items,
    int Page,
    int PageSize,
    bool HasMore
);
