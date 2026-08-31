using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Core.Services;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class CommentService : ICommentService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMentionResolver _mentionResolver;
    private readonly IPrivacyService _privacy;
    private readonly INotificationService? _notifications;

    public CommentService(OmnijoyDbContext db)
        : this(db, new MentionResolver(db), new PrivacyService(db), null)
    {
    }

    public CommentService(
        OmnijoyDbContext db,
        IMentionResolver mentionResolver,
        IPrivacyService privacy,
        INotificationService? notifications)
    {
        _db              = db;
        _mentionResolver = mentionResolver;
        _privacy         = privacy;
        _notifications   = notifications;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<CommentDto> CreateCommentAsync(
        Guid postId,
        Guid authorId,
        CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Comment content cannot be empty.");

        var parsedMentions = ParseMentionsOrThrow(request.Content);

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

        var mentions = await ResolveAllowedMentionsAsync(parsedMentions.Slugs, authorId);
        var mentionCreatedAt = DateTime.UtcNow;
        foreach (var mention in mentions)
        {
            _db.CommentMentions.Add(new CommentMention
            {
                CommentId = comment.Id,
                MentionedUserId = mention.UserId,
                MatchedSlug = mention.MatchedSlug,
                CreatedAt = mentionCreatedAt,
            });
        }

        await _db.SaveChangesAsync();

        await NotifyMentionsAsync(
            mentions.Where(mention => mention.UserId != authorId).Select(mention => mention.UserId),
            NotificationType.MentionInComment,
            comment.Id,
            authorId);

        return await LoadCommentDtoAsync(comment.Id)
            ?? throw new InvalidOperationException("Comment not found after creation.");
    }

    // ── Get paginated top-level comments ──────────────────────────────────────

    public async Task<PagedResult<CommentDto>> GetCommentsAsync(Guid postId, int page, int pageSize)
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
            .Include(c => c.Mentions).ThenInclude(m => m.MentionedUser)
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

        return new PagedResult<CommentDto>(
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
            .Include(c => c.Mentions).ThenInclude(m => m.MentionedUser)
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
            .Include(c => c.Mentions).ThenInclude(m => m.MentionedUser)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorId != requesterId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        var parsedMentions = ParseMentionsOrThrow(request.Content);

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;

        var resolvedMentions = await ResolveAllowedMentionsAsync(parsedMentions.Slugs, requesterId);
        var newlyMentionedUserIds = SynchronizeMentions(comment, resolvedMentions);

        await _db.SaveChangesAsync();

        await NotifyMentionsAsync(
            newlyMentionedUserIds.Where(userId => userId != requesterId),
            NotificationType.MentionInComment,
            comment.Id,
            requesterId);

        return await LoadCommentDtoAsync(comment.Id)
            ?? throw new InvalidOperationException("Comment not found after update.");
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

    private static MentionParseResult ParseMentionsOrThrow(string content)
    {
        var parsed = MentionParser.Parse(content);
        if (parsed.ExceedsLimit)
            throw new ArgumentException($"Content cannot mention more than {MentionParser.MaxDistinctMentions} distinct users.");
        return parsed;
    }

    private async Task<IReadOnlyList<ResolvedMention>> ResolveAllowedMentionsAsync(
        IEnumerable<string> slugs,
        Guid actorUserId)
    {
        var resolved = await _mentionResolver.ResolveUsersAsync(slugs);
        var allowed = new List<ResolvedMention>(resolved.Count);
        foreach (var mention in resolved)
        {
            if (mention.UserId == actorUserId ||
                await _privacy.AreNotBlockedAsync(actorUserId, mention.UserId))
            {
                allowed.Add(mention);
            }
        }

        return allowed;
    }

    private static IReadOnlyList<Guid> SynchronizeMentions(
        Comment comment,
        IReadOnlyList<ResolvedMention> resolvedMentions)
    {
        var oldUserIds = comment.Mentions.Select(mention => mention.MentionedUserId).ToHashSet();
        var newByUserId = resolvedMentions.ToDictionary(mention => mention.UserId);
        var now = DateTime.UtcNow;

        foreach (var existing in comment.Mentions.ToArray())
        {
            if (!newByUserId.TryGetValue(existing.MentionedUserId, out var replacement))
            {
                comment.Mentions.Remove(existing);
                continue;
            }

            if (!string.Equals(existing.MatchedSlug, replacement.MatchedSlug, StringComparison.Ordinal))
            {
                existing.MatchedSlug = replacement.MatchedSlug;
                existing.CreatedAt = now;
            }
        }

        foreach (var mention in resolvedMentions.Where(mention => !oldUserIds.Contains(mention.UserId)))
        {
            comment.Mentions.Add(new CommentMention
            {
                CommentId = comment.Id,
                MentionedUserId = mention.UserId,
                MatchedSlug = mention.MatchedSlug,
                CreatedAt = now,
            });
        }

        return resolvedMentions
            .Select(mention => mention.UserId)
            .Where(userId => !oldUserIds.Contains(userId))
            .ToArray();
    }

    private async Task NotifyMentionsAsync(
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        Guid referenceId,
        Guid actorUserId)
    {
        if (_notifications is null)
            return;

        foreach (var recipientUserId in recipientUserIds.Distinct())
        {
            await _notifications.CreateAsync(
                recipientUserId,
                type,
                referenceId.ToString(),
                actorUserId);
        }
    }

    private async Task<CommentDto?> LoadCommentDtoAsync(Guid commentId)
    {
        var comment = await _db.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Mentions).ThenInclude(m => m.MentionedUser)
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

        var mentions = comment.Mentions
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.MatchedSlug)
            .Select(m => new MentionDto(
                MatchedSlug: m.MatchedSlug,
                UserId: m.MentionedUserId,
                DisplayName: m.MentionedUser.DisplayName,
                UrlSlug: m.MentionedUser.UrlSlug))
            .ToArray();

        return new CommentDto(
            Id: comment.Id,
            PostId: comment.PostId,
            Author: author,
            ParentCommentId: comment.ParentCommentId,
            Content: comment.IsDeleted ? "[deleted]" : comment.Content,
            ReplyCount: replyCount,
            CreatedAt: comment.CreatedAt,
            UpdatedAt: comment.UpdatedAt,
            IsDeleted: comment.IsDeleted,
            ReactionsCount: 0,
            TopReactions: [],
            MyReaction: null,
            Mentions: mentions
        );
    }
}
