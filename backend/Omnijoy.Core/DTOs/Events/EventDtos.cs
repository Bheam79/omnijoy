namespace Omnijoy.Core.DTOs.Events;

// ── Sub-records ────────────────────────────────────────────────────────────────

public record EventCreatorDto(Guid Id, string DisplayName, string? AvatarUrl);

public record EventAttendeeDto(Guid UserId, string DisplayName, string? AvatarUrl, string RSVP);

// ── Event response ─────────────────────────────────────────────────────────────

public record EventDto(
    Guid Id,
    EventCreatorDto Creator,
    Guid? CompanyPageId,
    /// <summary>Display name of the organising company page; null for personal events.</summary>
    string? CompanyPageName,
    /// <summary>Logo URL of the organising company page; null for personal events.</summary>
    string? CompanyPageLogoUrl,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    string? CoverImageUrl,
    string Privacy,
    /// <summary>Current user's RSVP status; null if not authenticated or no RSVP recorded.</summary>
    string? MyRsvp,
    int GoingCount,
    int MaybeCount,
    int NotGoingCount,
    DateTime CreatedAt
);

// ── Create / Update requests ──────────────────────────────────────────────────

/// <summary>
/// JSON / form fields for POST /api/events.
/// Cover image is sent as a separate file field.
/// </summary>
public record CreateEventRequest(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    /// <summary>"Everyone" | "FriendsOfFriends" | "Friends" | "OnlyMe"</summary>
    string Privacy,
    Guid? CompanyPageId
);

public record UpdateEventRequest(
    string? Title,
    string? Description,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    string? Privacy
);

// ── RSVP ──────────────────────────────────────────────────────────────────────

public record RsvpRequest(
    /// <summary>"Going" | "Maybe" | "NotGoing"</summary>
    string Status
);

// ── Attendees ─────────────────────────────────────────────────────────────────

public record EventAttendeesResult(
    EventAttendeeDto[] Going,
    EventAttendeeDto[] Maybe,
    EventAttendeeDto[] NotGoing
);

// ── Paginated list ─────────────────────────────────────────────────────────────

public record EventsPageResult(
    EventDto[] Items,
    int Page,
    int PageSize,
    bool HasMore
);

// ── Cover image upload helper ─────────────────────────────────────────────────

/// <summary>
/// Wraps an uploaded cover-image stream so the Core interface stays free of
/// ASP.NET Core types.
/// </summary>
public record CoverImageUploadItem(Stream Content, string FileName, string ContentType);
