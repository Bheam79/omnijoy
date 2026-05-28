namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Centralised privacy-gate used by all services to decide whether an
/// action or piece of content is accessible to the requesting user.
/// All methods return <c>false</c> when any block relationship exists
/// between the two parties, regardless of other settings.
/// </summary>
public interface IPrivacyService
{
    /// <summary>
    /// Returns <c>true</c> when neither user has blocked the other.
    /// Blocked users are invisible to each other regardless of any other setting.
    /// </summary>
    Task<bool> AreNotBlockedAsync(Guid userA, Guid userB);

    /// <summary>Returns <c>true</c> when both users share an accepted friendship.</summary>
    Task<bool> AreFriendsAsync(Guid userA, Guid userB);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="requesterId"/> may view the profile
    /// of <paramref name="targetUserId"/> according to their <c>WhoCanSeeProfile</c>
    /// privacy setting. The profile owner always has access.
    /// </summary>
    Task<bool> CanViewProfileAsync(Guid targetUserId, Guid? requesterId);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="requesterId"/> may view
    /// <paramref name="targetUserId"/>'s posts according to their global
    /// <c>WhoCanSeePosts</c> setting.  Per-post privacy is enforced separately
    /// in <see cref="IPostService"/>.  The post author always has access.
    /// </summary>
    Task<bool> CanViewPostsAsync(Guid targetUserId, Guid? requesterId);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="requesterId"/> is allowed to initiate
    /// a new conversation with <paramref name="targetUserId"/> according to
    /// <c>WhoCanSendMessages</c> and block rules.
    /// </summary>
    Task<bool> CanSendMessagesAsync(Guid targetUserId, Guid requesterId);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="requesterId"/> may see the friend list
    /// of <paramref name="targetUserId"/> according to their <c>WhoCanSeeFriendList</c>
    /// setting. The list owner always has access.
    /// </summary>
    Task<bool> CanViewFriendListAsync(Guid targetUserId, Guid? requesterId);
}
