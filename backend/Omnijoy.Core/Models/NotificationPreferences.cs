namespace Omnijoy.Core.Models;

/// <summary>
/// Per-user notification toggles. One row per user, created with sensible
/// defaults (everything ON) at registration.
///
/// Boolean columns map to one or more <see cref="Enums.NotificationType"/>
/// values via <see cref="Omnijoy.Core.Services.NotificationPreferenceMap"/>.
/// </summary>
public class NotificationPreferences
{
    public Guid UserId { get; set; }

    // ── Post activity ─────────────────────────────────────────────────────────
    public bool LikesOnMyPosts { get; set; } = true;        // PostLike, CommentLike
    public bool CommentsOnMyPosts { get; set; } = true;     // PostComment, CommentReply
    public bool PostShares { get; set; } = true;            // PostShare

    // ── Social ────────────────────────────────────────────────────────────────
    public bool FriendRequests { get; set; } = true;        // FriendRequest, FriendRequestAccepted
    public bool NewFollower { get; set; } = true;           // NewFollower
    public bool Mentions { get; set; } = true;              // MentionInPost, MentionInComment, TaggedInPost
    public bool NewPostsFromFriends { get; set; } = true;   // NewPostFromFriend
    public bool FamilyRelations { get; set; } = true;       // FamilyRelationRequest

    // ── Events ────────────────────────────────────────────────────────────────
    public bool EventInvites { get; set; } = true;          // EventInvite, EventReminder, EventCreatedByFriend

    // ── System ────────────────────────────────────────────────────────────────
    public bool DirectMessages { get; set; } = true;        // NewMessage, MessageReceived
    public bool LiveStreams { get; set; } = true;           // LiveStreamStarted
    public bool CompanyPageInvites { get; set; } = true;    // CompanyPageInvite

    // Navigation property
    public User User { get; set; } = null!;
}
