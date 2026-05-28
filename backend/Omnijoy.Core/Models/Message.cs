using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderUserId { get; set; }
    public string? Content { get; set; }
    public MessageType MessageType { get; set; } = MessageType.Text;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public ICollection<MessageMedia> Media { get; set; } = [];
}
