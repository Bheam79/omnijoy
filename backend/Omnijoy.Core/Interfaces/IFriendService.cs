using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Friends;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for friend requests, the friend list, blocking, and family relations.
/// </summary>
public interface IFriendService
{
    // ── Request lifecycle ─────────────────────────────────────────────────────

    /// <summary>Sends a friend request from <paramref name="requesterId"/> to <paramref name="addresseeId"/>.</summary>
    Task<FriendRequestDto> SendRequestAsync(Guid requesterId, Guid addresseeId);

    /// <summary>Accepts a pending request that was sent by <paramref name="requesterId"/> to <paramref name="userId"/>.</summary>
    Task<FriendDto> AcceptRequestAsync(Guid userId, Guid requesterId);

    /// <summary>Declines a pending request sent by <paramref name="requesterId"/> to <paramref name="userId"/>.</summary>
    Task DeclineRequestAsync(Guid userId, Guid requesterId);

    /// <summary>Cancels a pending request that <paramref name="userId"/> sent to <paramref name="addresseeId"/>.</summary>
    Task CancelRequestAsync(Guid userId, Guid addresseeId);

    // ── Friendship management ─────────────────────────────────────────────────

    /// <summary>Removes an accepted friendship between <paramref name="userId"/> and <paramref name="otherUserId"/>.</summary>
    Task RemoveFriendAsync(Guid userId, Guid otherUserId);

    /// <summary>Blocks <paramref name="targetId"/> from <paramref name="userId"/>'s perspective.</summary>
    Task BlockUserAsync(Guid userId, Guid targetId);

    /// <summary>Unblocks a previously blocked user.</summary>
    Task UnblockUserAsync(Guid userId, Guid targetId);

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns paginated list of accepted friends for <paramref name="userId"/>.</summary>
    Task<PagedResult<FriendDto>> GetFriendsAsync(Guid userId, int page, int pageSize);

    /// <summary>
    /// Returns paginated list of accepted friends of <paramref name="targetUserId"/>
    /// as seen by <paramref name="viewerId"/>.  Throws <see cref="UnauthorizedAccessException"/>
    /// when the viewer is not allowed to see the friend list per privacy settings.
    /// </summary>
    Task<PagedResult<FriendDto>> GetUserFriendsAsync(Guid targetUserId, Guid? viewerId, int page, int pageSize);

    /// <summary>Returns pending friend requests directed at <paramref name="userId"/> (inbox).</summary>
    Task<FriendRequestDto[]> GetPendingRequestsAsync(Guid userId);

    /// <summary>Returns pending requests <paramref name="userId"/> has sent (outbox).</summary>
    Task<FriendRequestDto[]> GetSentRequestsAsync(Guid userId);

    /// <summary>Returns friend-of-friend suggestions for <paramref name="userId"/>.</summary>
    Task<FriendSuggestionDto[]> GetSuggestionsAsync(Guid userId, int limit = 10);

    /// <summary>Returns the friendship status between <paramref name="userId"/> and <paramref name="otherId"/>.</summary>
    Task<FriendStatusDto> GetFriendStatusAsync(Guid userId, Guid otherId);

    /// <summary>Returns the count of pending incoming friend requests (for nav badge).</summary>
    Task<int> GetPendingRequestCountAsync(Guid userId);

    // ── Family relations ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates or updates the family relation that <paramref name="userId"/> has with <paramref name="relatedUserId"/>.
    /// Pass null RelationType to remove the relation.
    /// The two users must be friends.
    /// </summary>
    Task<FamilyRelationDto?> SetFamilyRelationAsync(
        Guid userId,
        Guid relatedUserId,
        SetFamilyRelationRequest request);

    /// <summary>Returns the family relation <paramref name="userId"/> has labelled for <paramref name="relatedUserId"/>.</summary>
    Task<FamilyRelationDto?> GetFamilyRelationAsync(Guid userId, Guid relatedUserId);
}
