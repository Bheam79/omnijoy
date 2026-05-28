using Omnijoy.Core.DTOs.Admin;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Platform administration operations available to users with the Admin or
/// Moderator role. Per-method authorization is enforced at the controller layer.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Changes the platform role of <paramref name="targetUserId"/>.
    /// Admin-only at the controller layer; writes a <c>ChangeRole</c> entry to
    /// the moderation log.
    /// </summary>
    /// <param name="requesterId">ID of the Admin performing the change.</param>
    /// <param name="targetUserId">ID of the user whose role is being changed.</param>
    /// <param name="newRole">The new role string: "User" | "Moderator" | "Admin".</param>
    /// <returns>Updated admin user summary.</returns>
    /// <exception cref="KeyNotFoundException">Target user not found.</exception>
    /// <exception cref="ArgumentException">Unrecognised role string.</exception>
    Task<AdminUserDto> ChangeUserRoleAsync(Guid requesterId, Guid targetUserId, string newRole);

    /// <summary>
    /// Bans <paramref name="targetUserId"/>: sets <c>IsBanned = true</c>,
    /// records <c>BannedAt</c>, and writes a <c>BanUser</c> entry to the
    /// moderation log. Idempotent — banning an already-banned user is a no-op
    /// (but still logs the action).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Target user not found.</exception>
    Task<AdminUserDto> BanUserAsync(Guid requesterId, Guid targetUserId, string? notes);

    /// <summary>
    /// Unbans <paramref name="targetUserId"/>: clears <c>IsBanned</c> and
    /// <c>BannedAt</c>, and writes an <c>UnbanUser</c> entry to the moderation
    /// log. Idempotent — unbanning a user who is not banned is a no-op (but
    /// still logs the action).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Target user not found.</exception>
    Task<AdminUserDto> UnbanUserAsync(Guid requesterId, Guid targetUserId, string? notes);
}
