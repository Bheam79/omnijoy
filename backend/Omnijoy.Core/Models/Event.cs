using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class Event
{
    public Guid Id { get; set; }
    public Guid CreatorUserId { get; set; }
    public Guid? CompanyPageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    /// <summary>Human-readable location display string (kept as the primary display field).</summary>
    public string? Location { get; set; }

    // ── Structured venue address ───────────────────────────────────────────────

    /// <summary>Google Place ID for the venue.</summary>
    public string? LocationPlaceId { get; set; }

    /// <summary>City name.</summary>
    public string? LocationCity { get; set; }

    /// <summary>Country name.</summary>
    public string? LocationCountry { get; set; }

    /// <summary>Latitude for distance comparisons (decimal degrees, WGS-84).</summary>
    public decimal? LocationLatitude { get; set; }

    /// <summary>Longitude for distance comparisons (decimal degrees, WGS-84).</summary>
    public decimal? LocationLongitude { get; set; }

    public string? CoverImageUrl { get; set; }
    /// <summary>Optional external URL for purchasing tickets to this event.</summary>
    public string? TicketUrl { get; set; }
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Everyone;
    public EventPostingPolicy PostingPolicy { get; set; } = EventPostingPolicy.OrganizerOnly;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User CreatorUser { get; set; } = null!;
    public CompanyPage? CompanyPage { get; set; }
    public ICollection<EventAttendee> Attendees { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
}
