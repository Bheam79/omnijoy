namespace Omnijoy.Core.Models;

public class ConversationParticipant
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    /// <summary>Timestamp of the last message the user explicitly read. Used to compute unread counts.</summary>
    public DateTime? LastReadAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
