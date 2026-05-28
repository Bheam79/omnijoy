using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Services;

/// <summary>
/// Maps <see cref="NotificationType"/> values onto the boolean column of
/// <see cref="NotificationPreferences"/> that gates them.
///
/// Returns <c>true</c> when the user wants this notification, <c>false</c>
/// when it should be suppressed.
/// </summary>
public static class NotificationPreferenceMap
{
    public static bool IsAllowed(NotificationType type, NotificationPreferences prefs) => type switch
    {
        // Post activity
        NotificationType.PostLike             => prefs.LikesOnMyPosts,
        NotificationType.CommentLike          => prefs.LikesOnMyPosts,
        NotificationType.PostComment          => prefs.CommentsOnMyPosts,
        NotificationType.CommentReply         => prefs.CommentsOnMyPosts,
        NotificationType.PostShare            => prefs.PostShares,

        // Social
        NotificationType.FriendRequest         => prefs.FriendRequests,
        NotificationType.FriendRequestAccepted => prefs.FriendRequests,
        NotificationType.NewFollower           => prefs.NewFollower,
        NotificationType.MentionInPost         => prefs.Mentions,
        NotificationType.MentionInComment      => prefs.Mentions,
        NotificationType.TaggedInPost          => prefs.Mentions,
        NotificationType.NewPostFromFriend     => prefs.NewPostsFromFriends,
        NotificationType.FamilyRelationRequest => prefs.FamilyRelations,

        // Events
        NotificationType.EventInvite           => prefs.EventInvites,
        NotificationType.EventReminder         => prefs.EventInvites,
        NotificationType.EventCreatedByFriend  => prefs.EventInvites,

        // System
        NotificationType.NewMessage            => prefs.DirectMessages,
        NotificationType.MessageReceived       => prefs.DirectMessages,
        NotificationType.LiveStreamStarted     => prefs.LiveStreams,
        NotificationType.CompanyPageInvite     => prefs.CompanyPageInvites,

        // Unknown / future types: default to allowed.
        _ => true,
    };
}
