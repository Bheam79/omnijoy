namespace Omnijoy.Core.Models.Enums;

/// <summary>
/// Platform-level role for a user account.
/// Stored as a string column ("User" / "Moderator" / "Admin") so that
/// the database column is human-readable and extensible without renumbering.
/// </summary>
public enum UserRole
{
    /// <summary>Regular user — no elevated privileges.</summary>
    User = 0,

    /// <summary>
    /// Content moderator — can list and review user reports, take moderation
    /// actions, but cannot change other users' roles.
    /// </summary>
    Moderator = 1,

    /// <summary>
    /// Platform administrator — full access including role management.
    /// </summary>
    Admin = 2,
}
