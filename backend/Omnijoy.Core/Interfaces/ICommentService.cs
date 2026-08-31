using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Comments;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for threaded post comments (max 2 levels deep).
/// </summary>
public interface ICommentService
{
    /// <summary>
    /// Creates a comment on a post.  If <see cref="CreateCommentRequest.ParentCommentId"/> is set
    /// the comment is a reply; max depth is 1 (top-level + one reply level).
    /// </summary>
    Task<CommentDto> CreateCommentAsync(Guid postId, Guid authorId, CreateCommentRequest request);

    /// <summary>Returns paginated top-level comments for a post (ParentCommentId == null), newest first.</summary>
    Task<PagedResult<CommentDto>> GetCommentsAsync(
        Guid postId,
        int page,
        int pageSize,
        Guid? currentUserId = null);

    /// <summary>Returns all non-deleted replies for a top-level comment, oldest first.</summary>
    Task<CommentDto[]> GetRepliesAsync(Guid commentId);

    /// <summary>Updates the content of a comment owned by <paramref name="requesterId"/>.</summary>
    Task<CommentDto> UpdateCommentAsync(Guid commentId, Guid requesterId, UpdateCommentRequest request);

    /// <summary>Soft-deletes a comment (sets IsDeleted = true). Only the author may delete.</summary>
    Task DeleteCommentAsync(Guid commentId, Guid requesterId);
}
