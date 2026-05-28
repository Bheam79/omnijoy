using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class PostReaction
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public ReactionType ReactionType { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Post Post { get; set; } = null!;
    public User User { get; set; } = null!;
}
