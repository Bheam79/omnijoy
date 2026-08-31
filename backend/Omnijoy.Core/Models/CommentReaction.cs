using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class CommentReaction
{
    public Guid Id { get; set; }
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }
    public ReactionType ReactionType { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Comment Comment { get; set; } = null!;
    public User User { get; set; } = null!;
}
