using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Default <see cref="INotificationService"/> implementation:
/// persists notifications to the DB and pushes them in real time through
/// the NotificationHub (group <c>user:{userId}</c>).
///
/// The hub is referenced via the generic <c>IHubContext&lt;THub&gt;</c> abstraction
/// so this service has no compile-time dependency on the API project.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly OmnijoyDbContext _db;
    private readonly IHubContextDispatcher _hub;

    public NotificationService(OmnijoyDbContext db, IHubContextDispatcher hub)
    {
        _db  = db;
        _hub = hub;
    }

    // ── Create + push ─────────────────────────────────────────────────────────

    public async Task<NotificationDto> CreateAsync(
        Guid recipientUserId,
        NotificationType type,
        string? referenceId = null,
        Guid? actorUserId = null)
    {
        // Never notify yourself — silently skip (still return a DTO so callers stay simple).
        if (actorUserId == recipientUserId)
        {
            return new NotificationDto(
                Guid.Empty, type.ToString(), referenceId, true,
                DateTime.UtcNow, null, null, null);
        }

        var entity = new Notification
        {
            Id           = Guid.NewGuid(),
            UserId       = recipientUserId,
            Type         = type,
            ReferenceId  = referenceId,
            IsRead       = false,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync();

        NotificationActorDto? actor = null;
        if (actorUserId is { } actorId)
        {
            var u = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == actorId);
            if (u is not null)
                actor = new NotificationActorDto(u.Id, u.DisplayName, u.AvatarUrl);
        }

        var dto = new NotificationDto(
            Id:          entity.Id,
            Type:        entity.Type.ToString(),
            ReferenceId: entity.ReferenceId,
            IsRead:      entity.IsRead,
            CreatedAt:   entity.CreatedAt,
            Actor:       actor,
            Title:       null,
            Body:        null);

        // Push live event to the user's NotificationHub group.
        await _hub.SendToUserAsync(recipientUserId, "NotificationReceived", dto);

        return dto;
    }

    public async Task CreateForManyAsync(
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        string? referenceId = null,
        Guid? actorUserId = null)
    {
        var recipients = recipientUserIds.Where(id => id != actorUserId).Distinct().ToArray();
        if (recipients.Length == 0) return;

        var now = DateTime.UtcNow;
        var entities = recipients.Select(rid => new Notification
        {
            Id          = Guid.NewGuid(),
            UserId      = rid,
            Type        = type,
            ReferenceId = referenceId,
            IsRead      = false,
            CreatedAt   = now,
        }).ToArray();

        _db.Notifications.AddRange(entities);
        await _db.SaveChangesAsync();

        NotificationActorDto? actor = null;
        if (actorUserId is { } actorId)
        {
            var u = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == actorId);
            if (u is not null)
                actor = new NotificationActorDto(u.Id, u.DisplayName, u.AvatarUrl);
        }

        // One push per recipient (each gets their own NotificationId in the DTO).
        foreach (var e in entities)
        {
            var dto = new NotificationDto(
                Id:          e.Id,
                Type:        e.Type.ToString(),
                ReferenceId: e.ReferenceId,
                IsRead:      false,
                CreatedAt:   e.CreatedAt,
                Actor:       actor,
                Title:       null,
                Body:        null);

            await _hub.SendToUserAsync(e.UserId, "NotificationReceived", dto);
        }
    }

    public Task PushTransientAsync(Guid recipientUserId, string eventName, object payload)
        => _hub.SendToUserAsync(recipientUserId, eventName, payload);

    public async Task PushTransientToManyAsync(
        IEnumerable<Guid> recipientUserIds, string eventName, object payload)
    {
        foreach (var id in recipientUserIds.Distinct())
            await _hub.SendToUserAsync(id, eventName, payload);
    }

    // ── Inbox queries ─────────────────────────────────────────────────────────

    public async Task<NotificationPageResult> GetNotificationsAsync(
        Guid userId, int page, int pageSize, string[]? types = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Parse optional type filter — unrecognised type names are silently ignored.
        NotificationType[]? parsedTypes = null;
        if (types is { Length: > 0 })
        {
            parsedTypes = types
                .Select(t => Enum.TryParse<NotificationType>(t, ignoreCase: true, out var v)
                    ? (NotificationType?)v : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .Distinct()
                .ToArray();
        }

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (parsedTypes is { Length: > 0 })
            query = query.Where(n => parsedTypes.Contains(n.Type));

        query = query.OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync();

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Resolve actors. We use the ReferenceId for actor lookup only for friend-related
        // notification types; other types (post / message / event) store the entity id there.
        // We pre-build the actor map from a small batch query for performance.
        var friendActorIds = rows
            .Where(r => IsFriendType(r.Type) && Guid.TryParse(r.ReferenceId, out _))
            .Select(r => Guid.Parse(r.ReferenceId!))
            .Distinct()
            .ToArray();

        var actors = friendActorIds.Length == 0
            ? new Dictionary<Guid, User>()
            : await _db.Users
                .AsNoTracking()
                .Where(u => friendActorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

        var items = rows.Select(r =>
        {
            NotificationActorDto? actor = null;
            if (IsFriendType(r.Type) &&
                Guid.TryParse(r.ReferenceId, out var actorId) &&
                actors.TryGetValue(actorId, out var u))
            {
                actor = new NotificationActorDto(u.Id, u.DisplayName, u.AvatarUrl);
            }

            return new NotificationDto(
                Id:          r.Id,
                Type:        r.Type.ToString(),
                ReferenceId: r.ReferenceId,
                IsRead:      r.IsRead,
                CreatedAt:   r.CreatedAt,
                Actor:       actor,
                Title:       null,
                Body:        null);
        }).ToArray();

        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        return new NotificationPageResult(
            Items:       items,
            Page:        page,
            PageSize:    pageSize,
            HasMore:     (page * pageSize) < total,
            UnreadCount: unread);
    }

    public Task<int> GetUnreadCountAsync(Guid userId)
        => _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    // ── Mark-as-read ──────────────────────────────────────────────────────────

    public async Task MarkReadAsync(Guid notificationId, Guid userId)
    {
        var n = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (n is null || n.IsRead) return;

        n.IsRead = true;
        await _db.SaveChangesAsync();

        // Push an updated unread count so other connected tabs/devices stay in sync.
        var unread = await GetUnreadCountAsync(userId);
        await _hub.SendToUserAsync(userId, "UnreadCountChanged", new { unread });
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        var unreadRows = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        if (unreadRows.Count == 0) return;

        foreach (var n in unreadRows) n.IsRead = true;
        await _db.SaveChangesAsync();

        await _hub.SendToUserAsync(userId, "UnreadCountChanged", new { unread = 0 });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Notification types where the ReferenceId column stores the actor user id
    /// (friend request flow). Other types use ReferenceId for the target entity.
    /// </summary>
    private static bool IsFriendType(NotificationType t)
        => t is NotificationType.FriendRequest
             or NotificationType.FriendRequestAccepted
             or NotificationType.NewFollower
             or NotificationType.FamilyRelationRequest;
}

/// <summary>
/// Thin dispatcher abstraction so the service project does not need to reference
/// the API project's <see cref="Omnijoy.Api.Hubs.NotificationHub"/> directly.
/// The API project provides the concrete implementation that wraps
/// <see cref="IHubContext{THub}"/>.
/// </summary>
public interface IHubContextDispatcher
{
    Task SendToUserAsync(Guid userId, string eventName, object payload);
    Task SendToGroupAsync(string groupName, string eventName, object payload);
}
