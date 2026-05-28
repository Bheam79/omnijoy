using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class SharedPost
{
    public Guid Id { get; set; }

    /// <summary>The post that was shared.</summary>
    public Guid OriginalPostId { get; set; }

    /// <summary>The user who created the share.</summary>
    public Guid SharerId { get; set; }

    /// <summary>Where the share is posted: own wall, a friend's wall, or a company page.</summary>
    public ShareTargetType TargetType { get; set; }

    /// <summary>
    /// The user ID (FriendWall) or company page ID (CompanyPage) the share targets.
    /// Null for OwnWall shares.
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>Optional message the sharer adds when sharing.</summary>
    public string? OptionalMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────────
    public Post OriginalPost { get; set; } = null!;
    public User Sharer { get; set; } = null!;
}
