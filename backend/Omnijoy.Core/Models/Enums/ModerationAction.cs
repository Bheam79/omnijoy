namespace Omnijoy.Core.Models.Enums;

/// <summary>
/// Audit-trail action types written to the <c>ModerationLogs</c> table.
/// Stored as a human-readable string column (see <c>ModerationLogConfiguration</c>).
/// </summary>
public enum ModerationAction
{
    /// <summary>A report was marked as <c>Dismissed</c> by a moderator/admin.</summary>
    DismissReport = 0,

    /// <summary>A post was removed (soft-deleted) by a moderator/admin.</summary>
    RemovePost = 1,

    /// <summary>A comment was removed (soft-deleted) by a moderator/admin.</summary>
    RemoveComment = 2,

    /// <summary>A user account was banned.</summary>
    BanUser = 3,

    /// <summary>A user account was unbanned.</summary>
    UnbanUser = 4,

    /// <summary>A user's platform role was changed.</summary>
    ChangeRole = 5,

    /// <summary>A report was marked as <c>Reviewed</c> by a moderator/admin.</summary>
    ReviewReport = 6,
}
