namespace Omnijoy.Core.DTOs.Notifications;

/// <summary>
/// Per-user notification toggles returned by
/// <c>GET /api/account/notification-preferences</c> and accepted by
/// <c>PUT /api/account/notification-preferences</c>.
///
/// All fields default to true server-side when a row is missing or partially
/// supplied — i.e. "everything on" is the safe default.
/// </summary>
public record NotificationPreferencesDto(
    // Post activity
    bool LikesOnMyPosts,
    bool CommentsOnMyPosts,
    bool PostShares,

    // Social
    bool FriendRequests,
    bool NewFollower,
    bool Mentions,
    bool NewPostsFromFriends,
    bool FamilyRelations,

    // Events
    bool EventInvites,

    // System
    bool DirectMessages,
    bool LiveStreams,
    bool CompanyPageInvites
);
