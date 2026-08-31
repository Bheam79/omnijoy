namespace Omnijoy.Core.Models;

/// <summary>A user mention captured from a comment's content.</summary>
public class CommentMention
{
    public Guid CommentId { get; set; }
    public Guid MentionedUserId { get; set; }
    public string MatchedSlug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Comment Comment { get; set; } = null!;
    public User MentionedUser { get; set; } = null!;
}
