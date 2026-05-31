using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Events;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class EventService : IEventService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMediaStorageService _storage;
    private readonly IImageProcessingService _imageProcessor;

    public EventService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IImageProcessingService imageProcessor)
    {
        _db             = db;
        _storage        = storage;
        _imageProcessor = imageProcessor;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<EventDto> CreateEventAsync(
        Guid userId,
        CreateEventRequest request,
        CoverImageUploadItem? cover)
    {
        if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
            throw new ArgumentException($"Invalid Privacy: '{request.Privacy}'.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.");

        if (request.StartAt == default)
            throw new ArgumentException("StartAt is required.");

        // Validate company page membership when posting on behalf of a page
        if (request.CompanyPageId.HasValue)
        {
            var pageExists = await _db.CompanyPages.AnyAsync(p => p.Id == request.CompanyPageId.Value);
            if (!pageExists)
                throw new KeyNotFoundException($"Company page {request.CompanyPageId.Value} not found.");

            var isMember = await _db.CompanyPageAdmins.AnyAsync(a =>
                a.CompanyPageId == request.CompanyPageId.Value && a.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not an admin of that company page.");
        }

        string? coverUrl = null;
        if (cover is not null)
        {
            await using var processed = await _imageProcessor.ProcessImageAsync(cover.Content, ImageFolder.Cover);
            coverUrl = await _storage.StoreAsync(processed, "cover.webp", "events/covers");
        }

        var ev = new Event
        {
            Id          = Guid.NewGuid(),
            CreatorUserId = userId,
            CompanyPageId = request.CompanyPageId,
            Title       = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartAt     = request.StartAt,
            EndAt       = request.EndAt,
            Location    = request.Location?.Trim(),
            CoverImageUrl = coverUrl,
            Privacy     = privacy,
            CreatedAt   = DateTime.UtcNow,
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return await LoadEventDtoAsync(ev.Id, userId)
            ?? throw new InvalidOperationException("Event not found after creation.");
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<EventsPageResult> GetEventsAsync(
        Guid userId,
        string? filter,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var friendIds = await GetFriendIdsAsync(userId);
        var cutoff = DateTime.UtcNow.AddDays(-1); // include events that started within the past day

        IQueryable<Event> query = _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .Include(e => e.Attendees)
            .Where(e => e.StartAt >= cutoff);

        query = (filter?.ToLowerInvariant()) switch
        {
            "mine" => query.Where(e => e.CreatorUserId == userId),

            "friends" => query.Where(e =>
                friendIds.Contains(e.CreatorUserId) &&
                (e.Privacy == PrivacyLevel.Everyone ||
                 e.Privacy == PrivacyLevel.FriendsOfFriends ||
                 e.Privacy == PrivacyLevel.Friends)),

            "company" => query.Where(e =>
                e.CompanyPageId != null &&
                (e.Privacy == PrivacyLevel.Everyone ||
                 (friendIds.Contains(e.CreatorUserId) &&
                  (e.Privacy == PrivacyLevel.Friends || e.Privacy == PrivacyLevel.FriendsOfFriends)))),

            _ => query.Where(e =>
                // Own events
                e.CreatorUserId == userId ||
                // Friends' events visible to this user
                (friendIds.Contains(e.CreatorUserId) &&
                 (e.Privacy == PrivacyLevel.Everyone ||
                  e.Privacy == PrivacyLevel.FriendsOfFriends ||
                  e.Privacy == PrivacyLevel.Friends)) ||
                // Public events from anyone
                e.Privacy == PrivacyLevel.Everyone),
        };

        query = query.OrderBy(e => e.StartAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Fetch current user's RSVPs for these events in one query
        var eventIds = items.Select(e => e.Id).ToList();
        var myRsvps = await _db.EventAttendees
            .AsNoTracking()
            .Where(a => a.UserId == userId && eventIds.Contains(a.EventId))
            .ToDictionaryAsync(a => a.EventId, a => a.RSVP.ToString());

        var dtos = items.Select(e => MapToDto(e, myRsvps.GetValueOrDefault(e.Id))).ToArray();

        return new EventsPageResult(
            Items:    dtos,
            Page:     page,
            PageSize: pageSize,
            HasMore:  (page * pageSize) < total
        );
    }

    // ── Public list (unauthenticated) ─────────────────────────────────────────

    public async Task<EventsPageResult> GetPublicEventsAsync(
        string? location,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var cutoff = DateTime.UtcNow.AddDays(-1);

        IQueryable<Event> query = _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .Include(e => e.Attendees)
            .Where(e => e.Privacy == PrivacyLevel.Everyone)
            .Where(e => e.StartAt >= cutoff);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var needle = location.Trim();
            query = query.Where(e =>
                e.Location != null &&
                EF.Functions.Like(e.Location, $"%{needle}%"));
        }

        query = query.OrderBy(e => e.StartAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // No RSVP overlay — caller is unauthenticated.
        var dtos = items.Select(e => MapToDto(e, myRsvp: null)).ToArray();

        return new EventsPageResult(
            Items:    dtos,
            Page:     page,
            PageSize: pageSize,
            HasMore:  (page * pageSize) < total
        );
    }

    // ── Get single ────────────────────────────────────────────────────────────

    public async Task<EventDto> GetEventAsync(Guid eventId, Guid? requesterId)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .Include(e => e.Attendees)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        await EnforceReadAccessAsync(ev, requesterId);

        string? myRsvp = null;
        if (requesterId.HasValue)
        {
            var attendee = await _db.EventAttendees
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == requesterId.Value);
            myRsvp = attendee?.RSVP.ToString();
        }

        return MapToDto(ev, myRsvp);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<EventDto> UpdateEventAsync(
        Guid eventId,
        Guid requesterId,
        UpdateEventRequest request,
        CoverImageUploadItem? cover)
    {
        var ev = await _db.Events
            .Include(e => e.CreatorUser)
            .Include(e => e.Attendees)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        if (ev.CreatorUserId != requesterId)
            throw new UnauthorizedAccessException("Only the event creator may edit this event.");

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title cannot be empty.");
            ev.Title = request.Title.Trim();
        }

        if (request.Description is not null)
            ev.Description = request.Description.Trim();

        if (request.StartAt.HasValue)
            ev.StartAt = request.StartAt.Value;

        if (request.EndAt.HasValue)
            ev.EndAt = request.EndAt.Value;

        if (request.Location is not null)
            ev.Location = request.Location.Trim();

        if (request.Privacy is not null)
        {
            if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
                throw new ArgumentException($"Invalid Privacy: '{request.Privacy}'.");
            ev.Privacy = privacy;
        }

        if (cover is not null)
        {
            await using var processed = await _imageProcessor.ProcessImageAsync(cover.Content, ImageFolder.Cover);
            ev.CoverImageUrl = await _storage.StoreAsync(processed, "cover.webp", "events/covers");
        }

        await _db.SaveChangesAsync();

        var myRsvp = (await _db.EventAttendees
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == requesterId))
            ?.RSVP.ToString();

        return MapToDto(ev, myRsvp);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteEventAsync(Guid eventId, Guid requesterId)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        if (ev.CreatorUserId != requesterId)
            throw new UnauthorizedAccessException("Only the event creator may delete this event.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
    }

    // ── RSVP ──────────────────────────────────────────────────────────────────

    public async Task<EventDto> RsvpAsync(Guid eventId, Guid userId, string status)
    {
        if (!Enum.TryParse<RSVPStatus>(status, ignoreCase: true, out var rsvp))
            throw new ArgumentException($"Invalid RSVP status: '{status}'. Use Going, Maybe, or NotGoing.");

        // Ensure event exists
        var ev = await _db.Events
            .Include(e => e.CreatorUser)
            .Include(e => e.Attendees)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        var existing = ev.Attendees.FirstOrDefault(a => a.UserId == userId);
        if (existing is null)
        {
            _db.EventAttendees.Add(new EventAttendee
            {
                EventId = eventId,
                UserId  = userId,
                RSVP    = rsvp,
            });
        }
        else
        {
            existing.RSVP = rsvp;
        }

        await _db.SaveChangesAsync();

        // Reload to get fresh counts
        return await LoadEventDtoAsync(eventId, userId)
            ?? throw new InvalidOperationException("Event not found after RSVP.");
    }

    // ── Attendees ─────────────────────────────────────────────────────────────

    public async Task<EventAttendeesResult> GetAttendeesAsync(Guid eventId, Guid? requesterId)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        await EnforceReadAccessAsync(ev, requesterId);

        var attendees = await _db.EventAttendees
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.EventId == eventId)
            .ToListAsync();

        static EventAttendeeDto ToAttendeeDto(EventAttendee a) =>
            new(a.UserId, a.User.DisplayName, a.User.AvatarUrl, a.RSVP.ToString());

        return new EventAttendeesResult(
            Going:    attendees.Where(a => a.RSVP == RSVPStatus.Going)    .Select(ToAttendeeDto).ToArray(),
            Maybe:    attendees.Where(a => a.RSVP == RSVPStatus.Maybe)    .Select(ToAttendeeDto).ToArray(),
            NotGoing: attendees.Where(a => a.RSVP == RSVPStatus.NotGoing) .Select(ToAttendeeDto).ToArray()
        );
    }

    // ── Friend IDs ────────────────────────────────────────────────────────────

    public async Task<List<Guid>> GetFriendIdsAsync(Guid userId)
    {
        return await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<EventDto?> LoadEventDtoAsync(Guid eventId, Guid? requesterId)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .Include(e => e.Attendees)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ev is null) return null;

        string? myRsvp = null;
        if (requesterId.HasValue)
        {
            myRsvp = ev.Attendees
                .FirstOrDefault(a => a.UserId == requesterId.Value)
                ?.RSVP.ToString();
        }

        return MapToDto(ev, myRsvp);
    }

    private async Task EnforceReadAccessAsync(Event ev, Guid? requesterId)
    {
        if (requesterId.HasValue && ev.CreatorUserId == requesterId.Value)
            return; // creator always has access

        bool canRead = ev.Privacy switch
        {
            PrivacyLevel.Everyone => true,
            PrivacyLevel.OnlyMe   => false,
            PrivacyLevel.Friends or PrivacyLevel.FriendsOfFriends => requesterId.HasValue &&
                await _db.Friends.AnyAsync(f =>
                    f.Status == FriendStatus.Accepted &&
                    ((f.RequesterId == requesterId.Value && f.AddresseeId == ev.CreatorUserId) ||
                     (f.RequesterId == ev.CreatorUserId  && f.AddresseeId == requesterId.Value))),
            _ => false
        };

        if (!canRead)
            throw new UnauthorizedAccessException("You do not have permission to view this event.");
    }

    private static EventDto MapToDto(Event ev, string? myRsvp)
    {
        var creator = new EventCreatorDto(
            ev.CreatorUser.Id,
            ev.CreatorUser.DisplayName,
            ev.CreatorUser.AvatarUrl
        );

        int going    = ev.Attendees.Count(a => a.RSVP == RSVPStatus.Going);
        int maybe    = ev.Attendees.Count(a => a.RSVP == RSVPStatus.Maybe);
        int notGoing = ev.Attendees.Count(a => a.RSVP == RSVPStatus.NotGoing);

        return new EventDto(
            Id:            ev.Id,
            Creator:       creator,
            CompanyPageId: ev.CompanyPageId,
            Title:         ev.Title,
            Description:   ev.Description,
            StartAt:       ev.StartAt,
            EndAt:         ev.EndAt,
            Location:      ev.Location,
            CoverImageUrl: ev.CoverImageUrl,
            Privacy:       ev.Privacy.ToString(),
            MyRsvp:        myRsvp,
            GoingCount:    going,
            MaybeCount:    maybe,
            NotGoingCount: notGoing,
            CreatedAt:     ev.CreatedAt
        );
    }
}
