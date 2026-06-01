using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for post reactions (Like, Love, Haha, Wow, Sad, Angry).
/// </summary>
public interface IReactionService
{
    /// <summary>
    /// Returns the reaction counts per type and the current user's reaction (if any).
    /// Throws <see cref="KeyNotFoundException"/> if the post does not exist.
    /// </summary>
    Task<PostReactionsDto> GetReactionsAsync(Guid postId, Guid? currentUserId);

    /// <summary>
    /// Adds a new reaction or changes an existing one.
    /// Returns the updated reaction summary.
    /// Throws <see cref="KeyNotFoundException"/> if the post does not exist.
    /// Throws <see cref="ArgumentException"/> if <paramref name="reactionType"/> is invalid.
    /// </summary>
    Task<PostReactionsDto> AddOrUpdateReactionAsync(Guid postId, Guid userId, string reactionType);

    /// <summary>
    /// Removes the current user's reaction from a post (no-op if none).
    /// Returns the updated reaction summary.
    /// Throws <see cref="KeyNotFoundException"/> if the post does not exist.
    /// </summary>
    Task<PostReactionsDto> RemoveReactionAsync(Guid postId, Guid userId);

    /// <summary>
    /// Returns up to 5 people who reacted to the post, prioritising friends of
    /// <paramref name="currentUserId"/> (or the first 5 reactors if not authenticated).
    /// Also returns the count of reactors beyond the listed 5.
    /// Throws <see cref="KeyNotFoundException"/> if the post does not exist.
    /// </summary>
    Task<ReactionWhoDto> GetReactionWhoAsync(Guid postId, Guid? currentUserId);
}
