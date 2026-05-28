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
    public string? Location { get; set; }
    public string? CoverImageUrl { get; set; }
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Everyone;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User CreatorUser { get; set; } = null!;
    public CompanyPage? CompanyPage { get; set; }
    public ICollection<EventAttendee> Attendees { get; set; } = [];
}
