using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Notification persistence + real-time delivery via SignalR.
///
/// Each "CreateAsync" call writes a row to the Notifications table and
/// (when applicable) pushes a "NotificationReceived" event to the recipient's
/// NotificationHub group ("user:{userId}") so the bell badge updates live.
/// </summary>
public interface INotificationService
{
    // ── Create + push ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a notification for <paramref name="recipientUserId"/> and pushes
    /// it in real time via the NotificationHub. Returns the persisted DTO.
    /// </summary>
    /// <param name="recipientUserId">User who will receive the notification.</param>
    /// <param name="type">Notification type (controls the icon / wording on the client).</param>
    /// <param name="referenceId">Optional ID of the related entity (post, message, event, …).</param>
    /// <param name="actorUserId">Optional ID of the user who triggered it.</param>
    Task<NotificationDto> CreateAsync(
        Guid recipientUserId,
        NotificationType type,
        string? referenceId = null,
        Guid? actorUserId = null);

    /// <summary>
    /// Creates the same notification for many recipients in one go and pushes
    /// it to all of them. Used for "friend created a new post" fan-out.
    /// </summary>
    Task CreateForManyAsync(
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        string? referenceId = null,
        Guid? actorUserId = null);

    /// <summary>
    /// Pushes a transient event to a single user's NotificationHub group
    /// without persisting it (e.g. presence updates, message-received badge bumps).
    /// </summary>
    Task PushTransientAsync(Guid recipientUserId, string eventName, object payload);

    /// <summary>
    /// Pushes a transient event to many users' NotificationHub groups
    /// without persisting it.
    /// </summary>
    Task PushTransientToManyAsync(IEnumerable<Guid> recipientUserIds, string eventName, object payload);

    // ── Inbox queries ─────────────────────────────────────────────────────────

    /// <summary>Returns a paginated list of notifications for the given user (newest first).</summary>
    Task<NotificationPageResult> GetNotificationsAsync(Guid userId, int page, int pageSize);

    /// <summary>Returns the count of unread notifications for the bell-badge.</summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    // ── Mark-as-read ──────────────────────────────────────────────────────────

    /// <summary>Marks one notification as read. No-ops if it doesn't belong to the user.</summary>
    Task MarkReadAsync(Guid notificationId, Guid userId);

    /// <summary>Marks every unread notification for this user as read.</summary>
    Task MarkAllReadAsync(Guid userId);
}
