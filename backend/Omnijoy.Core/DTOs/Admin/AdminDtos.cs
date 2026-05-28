namespace Omnijoy.Core.DTOs.Admin;

// ── Requests ─────────────────────────────────────────────────────────────────

/// <summary>
/// Body for PATCH /api/admin/users/{id}/role.
/// Role must be one of: "User", "Moderator", "Admin".
/// </summary>
public record ChangeUserRoleRequest(string Role);

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
    DateTime CreatedAt
);
