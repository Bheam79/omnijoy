using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class Post
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? CompanyPageId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? BackgroundImageUrl { get; set; }
    public PostType PostType { get; set; } = PostType.Text;
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Friends;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public User Author { get; set; } = null!;
    public CompanyPage? CompanyPage { get; set; }
    public ICollection<PostMedia> Media { get; set; } = [];
}
