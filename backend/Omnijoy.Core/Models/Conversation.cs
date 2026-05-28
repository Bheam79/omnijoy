using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class Conversation
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; } = ConversationType.Direct;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
