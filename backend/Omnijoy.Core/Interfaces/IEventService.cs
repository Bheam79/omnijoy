using Omnijoy.Core.DTOs.Events;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for event creation, retrieval, updates, RSVPs, and listing.
/// </summary>
public interface IEventService
{
    /// <summary>Creates a new event with an optional cover image.</summary>
    Task<EventDto> CreateEventAsync(
        Guid userId,
        CreateEventRequest request,
        CoverImageUploadItem? cover);

    /// <summary>
    /// Returns upcoming events visible to <paramref name="userId"/>.
    /// <paramref name="filter"/>: "mine" | "friends" | "company" | null (default: all visible).
    /// </summary>
    Task<EventsPageResult> GetEventsAsync(
        Guid userId,
        string? filter,
        int page,
        int pageSize);

    /// <summary>
    /// Returns upcoming public events (Privacy = Everyone) ordered by start date.
    /// Optionally filtered by case-insensitive substring match on the event's
    /// Location field — used by the public front page to surface events near
    /// the visitor's location.
    /// Available without authentication.
    /// </summary>
    Task<EventsPageResult> GetPublicEventsAsync(
        string? location,
        int page,
        int pageSize);

    /// <summary>
    /// Returns a single event with the requester's RSVP status.
    /// Throws <see cref="KeyNotFoundException"/> when not found.
    /// Throws <see cref="UnauthorizedAccessException"/> when privacy denies access.
    /// </summary>
    Task<EventDto> GetEventAsync(Guid eventId, Guid? requesterId);

    /// <summary>Updates a mutable event field. Only the creator may update.</summary>
    Task<EventDto> UpdateEventAsync(
        Guid eventId,
        Guid requesterId,
        UpdateEventRequest request,
        CoverImageUploadItem? cover);

    /// <summary>Deletes an event. Only the creator may delete.</summary>
    Task DeleteEventAsync(Guid eventId, Guid requesterId);

    /// <summary>
    /// Creates or updates the current user's RSVP for an event.
    /// Returns the updated event DTO.
    /// </summary>
    Task<EventDto> RsvpAsync(Guid eventId, Guid userId, string status);

    /// <summary>Returns all attendees grouped by RSVP status.</summary>
    Task<EventAttendeesResult> GetAttendeesAsync(Guid eventId, Guid? requesterId);

    /// <summary>
    /// Returns accepted-friend user IDs for <paramref name="userId"/> — used by
    /// the controller to push EventCreated SignalR events to the right connections.
    /// </summary>
    Task<List<Guid>> GetFriendIdsAsync(Guid userId);
}
