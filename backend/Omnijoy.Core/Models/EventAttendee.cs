using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class EventAttendee
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public RSVPStatus RSVP { get; set; } = RSVPStatus.Going;

    // Navigation properties
    public Event Event { get; set; } = null!;
    public User User { get; set; } = null!;
}
