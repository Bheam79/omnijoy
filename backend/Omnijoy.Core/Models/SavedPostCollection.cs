namespace Omnijoy.Core.Models;

/// <summary>A user-owned folder for saved posts.</summary>
public class SavedPostCollection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<SavedPost> SavedPosts { get; set; } = [];
}
