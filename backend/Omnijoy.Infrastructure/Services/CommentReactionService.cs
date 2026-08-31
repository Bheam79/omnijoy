using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class CommentReactionService : ICommentReactionService
{
    private readonly OmnijoyDbContext _db;
    private readonly INotificationService _notifications;

    public CommentReactionService(OmnijoyDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<Guid> GetOwningPostIdAsync(Guid commentId)
        => (await GetActiveCommentAsync(commentId)).PostId;

    public async Task<PostReactionsDto> GetReactionsAsync(Guid commentId, Guid? currentUserId)
    {
        await GetActiveCommentAsync(commentId);
        return await BuildReactionsDtoAsync(commentId, currentUserId);
    }

    public async Task<PostReactionsDto> AddOrUpdateReactionAsync(
        Guid commentId,
        Guid userId,
        string reactionType)
    {
        var validNames = Enum.GetNames<ReactionType>();
        if (!validNames.Contains(reactionType, StringComparer.OrdinalIgnoreCase) ||
            !Enum.TryParse<ReactionType>(reactionType, ignoreCase: true, out var parsedType))
        {
            throw new ArgumentException($"Invalid ReactionType: '{reactionType}'. " +
                $"Valid values are: {string.Join(", ", validNames)}.");
        }

        var comment = await GetActiveCommentAsync(commentId);
        var existing = await _db.CommentReactions
            .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId);

        if (existing is null)
        {
            _db.CommentReactions.Add(new CommentReaction
            {
                Id = Guid.NewGuid(),
                CommentId = commentId,
                UserId = userId,
                ReactionType = parsedType,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.ReactionType = parsedType;
        }

        await _db.SaveChangesAsync();

        // NotificationService owns preference handling and self-suppression. Call it
        // for every successful add/change rather than duplicating those rules here.
        await _notifications.CreateAsync(
            comment.AuthorId,
            NotificationType.CommentLike,
            commentId.ToString(),
            userId);

        return await BuildReactionsDtoAsync(commentId, userId);
    }

    public async Task<PostReactionsDto> RemoveReactionAsync(Guid commentId, Guid userId)
    {
        await GetActiveCommentAsync(commentId);

        var existing = await _db.CommentReactions
            .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId)
            ?? throw new KeyNotFoundException(
                $"Reaction on comment {commentId} by user {userId} not found.");

        _db.CommentReactions.Remove(existing);
        await _db.SaveChangesAsync();

        return await BuildReactionsDtoAsync(commentId, userId);
    }

    public async Task<ReactionWhoDto> GetReactionWhoAsync(Guid commentId, Guid? currentUserId)
    {
        await GetActiveCommentAsync(commentId);

        var reactors = await _db.CommentReactions
            .AsNoTracking()
            .Where(r => r.CommentId == commentId)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.UserId)
            .Select(r => new
            {
                r.UserId,
                r.User.DisplayName,
                r.User.AvatarUrl,
                r.ReactionType,
                r.CreatedAt,
            })
            .ToListAsync();

        if (reactors.Count == 0)
            return new ReactionWhoDto([], 0);

        HashSet<Guid> friendIds = [];
        if (currentUserId.HasValue)
        {
            var userId = currentUserId.Value;
            var ids = await _db.Friends
                .AsNoTracking()
                .Where(f => f.Status == FriendStatus.Accepted &&
                            (f.RequesterId == userId || f.AddresseeId == userId))
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();
            friendIds = [.. ids];
        }

        const int maxListed = 5;
        var listed = reactors
            .OrderByDescending(r => friendIds.Contains(r.UserId))
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.UserId)
            .Take(maxListed)
            .ToArray();

        var people = listed.Select(r => new ReactionWhoUserDto(
            r.UserId,
            r.DisplayName,
            r.AvatarUrl,
            friendIds.Contains(r.UserId),
            r.ReactionType.ToString()))
            .ToArray();

        return new ReactionWhoDto(people, reactors.Count - listed.Length);
    }

    private async Task<Comment> GetActiveCommentAsync(Guid commentId)
        => await _db.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

    private async Task<PostReactionsDto> BuildReactionsDtoAsync(
        Guid commentId,
        Guid? currentUserId)
    {
        var reactions = await _db.CommentReactions
            .AsNoTracking()
            .Where(r => r.CommentId == commentId)
            .ToListAsync();

        var counts = reactions
            .GroupBy(r => r.ReactionType)
            .OrderBy(g => g.Key)
            .Select(g => new ReactionCountDto(g.Key.ToString(), g.Count()))
            .ToArray();

        var currentUserReaction = currentUserId.HasValue
            ? reactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType.ToString()
            : null;

        return new PostReactionsDto(counts, reactions.Count, currentUserReaction);
    }
}
