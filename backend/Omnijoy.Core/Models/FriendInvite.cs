using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

/// <summary>
/// Represents a friend invitation, either email-targeted or link-based.
/// </summary>
public class FriendInvite
{
    public Guid Id { get; set; }

    /// <summary>The user who created the invite.</summary>
    public Guid InviterId { get; set; }

    /// <summary>Unique URL-safe token used in the invite link.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Target email address for email invites.
    /// Null for general link-based invites.
    /// </summary>
    public string? Email { get; set; }

    public FriendInviteStatus Status { get; set; } = FriendInviteStatus.Pending;

    /// <summary>When the invite expires (default: 30 days after creation).</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set once the invite is accepted.</summary>
    public Guid? AcceptedByUserId { get; set; }

    /// <summary>When the invite was accepted.</summary>
    public DateTime? AcceptedAt { get; set; }

    // Navigation properties
    public User Inviter { get; set; } = null!;
    public User? AcceptedBy { get; set; }
}
