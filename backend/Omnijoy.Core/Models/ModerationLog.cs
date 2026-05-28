using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

/// <summary>
/// Append-only audit-trail row recording a single moderation action.
/// Written by <c>IModerationLogService.LogAsync</c> at the time the action takes effect.
/// </summary>
public class ModerationLog
{
    public Guid Id { get; set; }

    /// <summary>The user (moderator/admin) who performed the action.</summary>
    public Guid ActorId { get; set; }

    public ModerationAction Action { get; set; }

    /// <summary>
    /// Free-form string identifying the kind of target (e.g. "User", "Post",
    /// "Comment", "Report"). Stored as a string so new target kinds can be
    /// added without a schema change.
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// String representation of the target's primary key. Stored as a string
    /// (rather than a Guid) so future targets with non-Guid IDs can use the
    /// same column.
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Optional free-form notes describing the action.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public User Actor { get; set; } = null!;
}
