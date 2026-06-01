using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Events;
using Omnijoy.Core.DTOs.Posts;
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

        var postingPolicy = EventPostingPolicy.OrganizerOnly;
        if (request.PostingPolicy is not null &&
            !Enum.TryParse<EventPostingPolicy>(request.PostingPolicy, ignoreCase: true, out postingPolicy))
            throw new ArgumentException($"Invalid PostingPolicy: '{request.PostingPolicy}'.");

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
            Id            = Guid.NewGuid(),
            CreatorUserId = userId,
            CompanyPageId = request.CompanyPageId,
            Title         = request.Title.Trim(),
            Description   = request.Description?.Trim(),
            StartAt       = request.StartAt,
            EndAt         = request.EndAt,
            Location      = request.Location?.Trim(),
            CoverImageUrl = coverUrl,
            Privacy       = privacy,
            PostingPolicy = postingPolicy,
            CreatedAt     = DateTime.UtcNow,
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
        int pageSize,
        Guid? companyPageId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var friendIds = await GetFriendIdsAsync(userId);
        var cutoff = DateTime.UtcNow.AddDays(-1); // include events that started within the past day

        IQueryable<Event> query = _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .Include(e => e.CompanyPage)
            .Include(e => e.Attendees)
            .Where(e => e.StartAt >= cutoff);

        // When scoped to a specific company page, show all its events regardless of other filters
        if (companyPageId.HasValue)
        {
            query = query.Where(e => e.CompanyPageId == companyPageId.Value);
            var total2 = await query.CountAsync();
            var items2 = await query
                .OrderBy(e => e.StartAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var eventIds2 = items2.Select(e => e.Id).ToList();
            var myRsvps2 = await _db.EventAttendees
                .AsNoTracking()
                .Where(a => a.UserId == userId && eventIds2.Contains(a.EventId))
                .ToDictionaryAsync(a => a.EventId, a => a.RSVP.ToString());

            var dtos2 = items2.Select(e => MapToDto(e, myRsvps2.GetValueOrDefault(e.Id))).ToArray();
            return new EventsPageResult(
                Items:    dtos2,
                Page:     page,
                PageSize: pageSize,
                HasMore:  (page * pageSize) < total2
            );
        }

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
            .Include(e => e.CompanyPage)
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
            .Include(e => e.CompanyPage)
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
            .Include(e => e.CompanyPage)
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

        if (request.PostingPolicy is not null)
        {
            if (!Enum.TryParse<EventPostingPolicy>(request.PostingPolicy, ignoreCase: true, out var pp))
                throw new ArgumentException($"Invalid PostingPolicy: '{request.PostingPolicy}'.");
            ev.PostingPolicy = pp;
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
            .Include(e => e.CompanyPage)
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

    // ── Event posts ───────────────────────────────────────────────────────────

    public async Task<EventPostsPageResult> GetEventPostsAsync(
        Guid eventId,
        Guid? requesterId,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var ev = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        await EnforceReadAccessAsync(ev, requesterId);

        var query = _db.Posts
            .AsNoTracking()
            .Where(p => p.EventId == eventId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt);

        var total = await query.CountAsync();
        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Author)
            .Include(p => p.Media)
            .ToListAsync();

        var items = posts.Select(MapPostToDto).ToArray();
        return new EventPostsPageResult(items, page, pageSize, (page * pageSize) < total);
    }

    public async Task<PostDto> CreateEventPostAsync(
        Guid eventId,
        Guid authorId,
        CreateEventPostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content is required.");

        var ev = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        await EnforceReadAccessAsync(ev, authorId);

        // Enforce posting policy
        if (ev.PostingPolicy == EventPostingPolicy.OrganizerOnly)
        {
            bool isOrganizer = ev.CreatorUserId == authorId;
            if (!isOrganizer && ev.CompanyPageId.HasValue)
            {
                isOrganizer = await _db.CompanyPageAdmins.AnyAsync(a =>
                    a.CompanyPageId == ev.CompanyPageId.Value && a.UserId == authorId);
            }
            if (!isOrganizer)
                throw new UnauthorizedAccessException("Only the event organiser may post on this event wall.");
        }

        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = authorId,
            EventId      = eventId,
            Content      = request.Content.Trim(),
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        // Reload with navigation props
        var loaded = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstAsync(p => p.Id == post.Id);

        return MapPostToDto(loaded);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<EventDto?> LoadEventDtoAsync(Guid eventId, Guid? requesterId)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.CreatorUser)
            .Include(e => e.CompanyPage)
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

    private static PostDto MapPostToDto(Post post)
    {
        var author = new PostAuthorDto(
            post.Author.Id,
            post.Author.DisplayName,
            post.Author.AvatarUrl);

        var media = post.Media
            .OrderBy(m => m.Order)
            .Select(m => new PostMediaItemDto(m.Id, m.MediaType.ToString(), m.Url, m.ThumbnailUrl, m.Order))
            .ToArray();

        return new PostDto(
            Id:                 post.Id,
            Author:             author,
            CompanyPageId:      post.CompanyPageId,
            Content:            post.Content,
            BackgroundImageUrl: post.BackgroundImageUrl,
            PostType:           post.PostType.ToString(),
            Privacy:            post.Privacy.ToString(),
            Media:              media,
            LinkPreview:        null,
            CreatedAt:          post.CreatedAt,
            UpdatedAt:          post.UpdatedAt);
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
            Id:                  ev.Id,
            Creator:             creator,
            CompanyPageId:       ev.CompanyPageId,
            CompanyPageName:     ev.CompanyPage?.Name,
            CompanyPageLogoUrl:  ev.CompanyPage?.LogoUrl,
            Title:               ev.Title,
            Description:         ev.Description,
            StartAt:             ev.StartAt,
            EndAt:               ev.EndAt,
            Location:            ev.Location,
            CoverImageUrl:       ev.CoverImageUrl,
            Privacy:             ev.Privacy.ToString(),
            PostingPolicy:       ev.PostingPolicy.ToString(),
            MyRsvp:              myRsvp,
            GoingCount:          going,
            MaybeCount:          maybe,
            NotGoingCount:       notGoing,
            CreatedAt:           ev.CreatedAt
        );
    }
}
