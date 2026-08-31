namespace Omnijoy.Core.Models;

/// <summary>A user mention captured from a post's content.</summary>
public class PostMention
{
    public Guid PostId { get; set; }
    public Guid MentionedUserId { get; set; }
    public string MatchedSlug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Post Post { get; set; } = null!;
    public User MentionedUser { get; set; } = null!;
}
