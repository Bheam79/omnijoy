namespace Omnijoy.Core.Models;

/// <summary>A private bookmark owned by a user.</summary>
public class SavedPost
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PostId { get; set; }
    public Guid? CollectionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Post Post { get; set; } = null!;
    public SavedPostCollection? Collection { get; set; }
}
