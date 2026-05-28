using Omnijoy.Core.DTOs.Admin;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Platform administration operations available to users with the Admin role.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Changes the platform role of <paramref name="targetUserId"/>.
    /// </summary>
    /// <param name="requesterId">ID of the Admin performing the change.</param>
    /// <param name="targetUserId">ID of the user whose role is being changed.</param>
    /// <param name="newRole">The new role string: "User" | "Moderator" | "Admin".</param>
    /// <returns>Updated admin user summary.</returns>
    /// <exception cref="KeyNotFoundException">Target user not found.</exception>
    /// <exception cref="ArgumentException">Unrecognised role string.</exception>
    Task<AdminUserDto> ChangeUserRoleAsync(Guid requesterId, Guid targetUserId, string newRole);
}
