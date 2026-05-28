using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Append-only audit log for moderator/admin actions. Implementations write
/// to the <c>ModerationLogs</c> table. Reads are paginated and filterable.
/// </summary>
public interface IModerationLogService
{
    /// <summary>
    /// Records a single moderation action. The row is written immediately
    /// (<c>SaveChangesAsync</c>) so the caller does not need to commit
    /// separately.
    /// </summary>
    /// <param name="actorId">The moderator/admin who performed the action.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="targetType">String tag identifying the target kind
    /// (e.g. <c>"User"</c>, <c>"Post"</c>, <c>"Comment"</c>, <c>"Report"</c>).</param>
    /// <param name="targetId">String form of the target's primary key.</param>
    /// <param name="notes">Optional free-form notes describing the action.</param>
    /// <returns>The ID of the newly-written log row.</returns>
    Task<Guid> LogAsync(
        Guid actorId,
        ModerationAction action,
        string targetType,
        string targetId,
        string? notes = null);

    /// <summary>
    /// Returns a paginated, optionally-filtered view of the audit log,
    /// most recent first. <paramref name="actorId"/> and <paramref name="action"/>
    /// are optional filters; pass <c>null</c> for "no filter".
    /// </summary>
    Task<ModerationLogListResult> ListAsync(
        Guid? actorId,
        string? action,
        int page,
        int pageSize);
}
