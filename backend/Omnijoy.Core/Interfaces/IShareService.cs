using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for sharing posts to own wall, a friend's wall, or a company page.
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Creates a new share of <paramref name="postId"/> by <paramref name="sharerId"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Original post not found or not visible.</exception>
    /// <exception cref="UnauthorizedAccessException">Sharer may not view or share this post.</exception>
    /// <exception cref="ArgumentException">Invalid target type or missing target ID.</exception>
    Task<SharedPostFeedItemDto> CreateShareAsync(Guid sharerId, Guid postId, CreateShareRequest request);

    /// <summary>
    /// Returns the user IDs to notify / push feed events to when a share is created.
    /// For OwnWall: sharer's friends + sharer.
    /// For FriendWall: target user's friends + target user + sharer.
    /// For CompanyPage: page followers + sharer.
    /// </summary>
    Task<List<Guid>> GetShareRecipientIdsAsync(Guid sharerId, SharedPostFeedItemDto share);
}
