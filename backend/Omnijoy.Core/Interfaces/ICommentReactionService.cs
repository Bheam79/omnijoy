using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for comment reactions (Like, Love, Haha, Wow, Sad, Angry).
/// </summary>
public interface ICommentReactionService
{
    /// <summary>
    /// Returns the post that owns an active comment.
    /// Throws <see cref="KeyNotFoundException"/> if the comment does not exist.
    /// </summary>
    Task<Guid> GetOwningPostIdAsync(Guid commentId);

    /// <summary>
    /// Returns the reaction counts per type and the current user's reaction (if any).
    /// Throws <see cref="KeyNotFoundException"/> if the comment does not exist.
    /// </summary>
    Task<PostReactionsDto> GetReactionsAsync(Guid commentId, Guid? currentUserId);

    /// <summary>
    /// Returns up to 5 people who reacted to the comment, prioritising friends of
    /// <paramref name="currentUserId"/> (or the first 5 reactors if not authenticated).
    /// Also returns the count of reactors beyond the listed 5.
    /// Throws <see cref="KeyNotFoundException"/> if the comment does not exist.
    /// </summary>
    Task<ReactionWhoDto> GetReactionWhoAsync(Guid commentId, Guid? currentUserId);

    /// <summary>
    /// Adds a new reaction or changes an existing one and returns the updated summary.
    /// Throws <see cref="KeyNotFoundException"/> if the comment does not exist.
    /// Throws <see cref="ArgumentException"/> if <paramref name="reactionType"/> is invalid.
    /// </summary>
    Task<PostReactionsDto> AddOrUpdateReactionAsync(Guid commentId, Guid userId, string reactionType);

    /// <summary>
    /// Removes the current user's reaction and returns the updated summary.
    /// Throws <see cref="KeyNotFoundException"/> if the comment or reaction does not exist.
    /// </summary>
    Task<PostReactionsDto> RemoveReactionAsync(Guid commentId, Guid userId);
}
