using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class Post
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? CompanyPageId { get; set; }
    /// <summary>Set when the post belongs to an event wall; null for regular feed posts.</summary>
    public Guid? EventId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? BackgroundImageUrl { get; set; }
    public PostType PostType { get; set; } = PostType.Text;
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Friends;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // ── Embedded URL preview (OG / Twitter Card metadata) ──────────────────────
    // When a user pastes a URL into the composer the frontend fetches the
    // preview via /api/meta-preview and submits these fields alongside the post.
    public string? LinkUrl { get; set; }
    public string? LinkTitle { get; set; }
    public string? LinkDescription { get; set; }
    public string? LinkImageUrl { get; set; }
    public string? LinkSiteName { get; set; }

    // Navigation properties
    public User Author { get; set; } = null!;
    public CompanyPage? CompanyPage { get; set; }
    public Event? Event { get; set; }
    public ICollection<PostMedia> Media { get; set; } = [];
    public ICollection<PostReaction> Reactions { get; set; } = [];
    public ICollection<SavedPost> SavedBy { get; set; } = [];
}
