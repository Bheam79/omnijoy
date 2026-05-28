namespace Omnijoy.Core.DTOs.Notifications;

/// <summary>
/// A notification entry sent to a user (e.g. friend request, mention, live stream start).
/// </summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string? ReferenceId,
    bool IsRead,
    DateTime CreatedAt,
    NotificationActorDto? Actor,
    string? Title,
    string? Body
);

/// <summary>Minimal info about the user who triggered the notification (if any).</summary>
public record NotificationActorDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl
);

/// <summary>Result of a paginated notifications query.</summary>
public record NotificationPageResult(
    NotificationDto[] Items,
    int Page,
    int PageSize,
    bool HasMore,
    int UnreadCount
);

/// <summary>Presence status for a single user.</summary>
public record UserPresenceDto(
    Guid UserId,
    bool IsOnline,
    DateTime? LastSeenAt
);
