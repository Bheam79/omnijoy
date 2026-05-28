using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class CommentService : ICommentService
{
    private readonly OmnijoyDbContext _db;

    public CommentService(OmnijoyDbContext db)
    {
        _db = db;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<CommentDto> CreateCommentAsync(
        Guid postId,
        Guid authorId,
        CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Comment content cannot be empty.");

        // Verify post exists
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Post {postId} not found.");

        // Validate parent comment
        if (request.ParentCommentId.HasValue)
        {
            var parent = await _db.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentCommentId.Value && !c.IsDeleted);

            if (parent is null)
                throw new KeyNotFoundException($"Parent comment {request.ParentCommentId.Value} not found.");

            // Depth check: parent must be a top-level comment (ParentCommentId == null)
            if (parent.ParentCommentId.HasValue)
                throw new InvalidOperationException("Cannot reply to a reply. Maximum comment depth is 2 levels.");

            // Parent must belong to the same post
            if (parent.PostId != postId)
                throw new InvalidOperationException("Parent comment does not belong to the specified post.");
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            ParentCommentId = request.ParentCommentId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        return await LoadCommentDtoAsync(comment.Id)
            ?? throw new InvalidOperationException("Comment not found after creation.");
    }

    // ── Get paginated top-level comments ──────────────────────────────────────

    public async Task<CommentsPageResult> GetCommentsAsync(Guid postId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Post {postId} not found.");

        var query = _db.Comments
            .AsNoTracking()
            .Where(c => c.PostId == postId && c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync();

        var comments = await query
            .Include(c => c.Author)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load reply counts in a single batch query
        var commentIds = comments.Select(c => c.Id).ToList();
        var replyCounts = await _db.Comments
            .AsNoTracking()
            .Where(c => c.ParentCommentId != null && commentIds.Contains(c.ParentCommentId.Value) && !c.IsDeleted)
            .GroupBy(c => c.ParentCommentId!.Value)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.Count);

        var dtos = comments
            .Select(c => MapToDto(c, replyCounts.GetValueOrDefault(c.Id, 0)))
            .ToArray();

        return new CommentsPageResult(
            Items: dtos,
            Page: page,
            PageSize: pageSize,
            HasMore: (page * pageSize) < totalCount
        );
    }

    // ── Get replies ───────────────────────────────────────────────────────────

    public async Task<CommentDto[]> GetRepliesAsync(Guid commentId)
    {
        var comment = await _db.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

        if (comment is null)
            throw new KeyNotFoundException($"Comment {commentId} not found.");

        var replies = await _db.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.ParentCommentId == commentId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return replies.Select(c => MapToDto(c, 0)).ToArray();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<CommentDto> UpdateCommentAsync(
        Guid commentId,
        Guid requesterId,
        UpdateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Comment content cannot be empty.");

        var comment = await _db.Comments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorId != requesterId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Load reply count
        var replyCount = await _db.Comments
            .CountAsync(c => c.ParentCommentId == commentId && !c.IsDeleted);

        return MapToDto(comment, replyCount);
    }

    // ── Delete (soft) ─────────────────────────────────────────────────────────

    public async Task DeleteCommentAsync(Guid commentId, Guid requesterId)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorId != requesterId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<CommentDto?> LoadCommentDtoAsync(Guid commentId)
    {
        var comment = await _db.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment is null) return null;

        var replyCount = await _db.Comments
            .CountAsync(c => c.ParentCommentId == commentId && !c.IsDeleted);

        return MapToDto(comment, replyCount);
    }

    private static CommentDto MapToDto(Comment comment, int replyCount)
    {
        var author = new CommentAuthorDto(
            comment.Author.Id,
            comment.Author.DisplayName,
            comment.Author.AvatarUrl
        );

        return new CommentDto(
            Id: comment.Id,
            PostId: comment.PostId,
            Author: author,
            ParentCommentId: comment.ParentCommentId,
            Content: comment.IsDeleted ? "[deleted]" : comment.Content,
            ReplyCount: replyCount,
            CreatedAt: comment.CreatedAt,
            UpdatedAt: comment.UpdatedAt,
            IsDeleted: comment.IsDeleted
        );
    }
}
