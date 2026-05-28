using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for post creation, retrieval, feed assembly, and deletion.
/// </summary>
public interface IPostService
{
    /// <summary>
    /// Creates a new post (with optional media uploads).
    /// Returns the persisted post DTO.
    /// </summary>
    Task<PostDto> CreatePostAsync(
        Guid authorId,
        CreatePostRequest request,
        IReadOnlyList<MediaUploadItem>? mediaItems);

    /// <summary>
    /// Returns a paginated, privacy-filtered feed for the given user:
    /// own posts + accepted friends' posts visible to this user.
    /// </summary>
    Task<FeedPageResult> GetFeedAsync(Guid userId, int page, int pageSize);

    /// <summary>
    /// Returns a single post, respecting privacy rules.
    /// Throws <see cref="KeyNotFoundException"/> if the post doesn't exist.
    /// Throws <see cref="UnauthorizedAccessException"/> if the requester may not view it.
    /// </summary>
    Task<PostDto> GetPostAsync(Guid postId, Guid? requesterId);

    /// <summary>Updates the content and/or privacy of a post owned by <paramref name="requesterId"/>.</summary>
    Task<PostDto> UpdatePostAsync(Guid postId, Guid requesterId, UpdatePostRequest request);

    /// <summary>Soft-deletes a post (sets DeletedAt). Only the owner may delete.</summary>
    Task DeletePostAsync(Guid postId, Guid requesterId);

    /// <summary>
    /// Returns accepted-friend user IDs for <paramref name="userId"/> — used by
    /// the controller to push NewPost SignalR events to the right connections.
    /// </summary>
    Task<List<Guid>> GetFriendIdsAsync(Guid userId);
}
