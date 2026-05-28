using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class ReactionService : IReactionService
{
    private readonly OmnijoyDbContext _db;

    public ReactionService(OmnijoyDbContext db)
    {
        _db = db;
    }

    // ── Get reactions ─────────────────────────────────────────────────────────

    public async Task<PostReactionsDto> GetReactionsAsync(Guid postId, Guid? currentUserId)
    {
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Post {postId} not found.");

        return await BuildReactionsDtoAsync(postId, currentUserId);
    }

    // ── Add or update ─────────────────────────────────────────────────────────

    public async Task<PostReactionsDto> AddOrUpdateReactionAsync(
        Guid postId,
        Guid userId,
        string reactionType)
    {
        if (!Enum.TryParse<ReactionType>(reactionType, ignoreCase: true, out var parsedType))
            throw new ArgumentException($"Invalid ReactionType: '{reactionType}'. " +
                $"Valid values are: {string.Join(", ", Enum.GetNames<ReactionType>())}.");

        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Post {postId} not found.");

        var existing = await _db.PostReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

        if (existing is null)
        {
            _db.PostReactions.Add(new PostReaction
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = userId,
                ReactionType = parsedType,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.ReactionType = parsedType;
            // Preserve original CreatedAt (don't update it — this is a "change" not a new reaction)
        }

        await _db.SaveChangesAsync();

        return await BuildReactionsDtoAsync(postId, userId);
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    public async Task<PostReactionsDto> RemoveReactionAsync(Guid postId, Guid userId)
    {
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Post {postId} not found.");

        var existing = await _db.PostReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

        if (existing is not null)
        {
            _db.PostReactions.Remove(existing);
            await _db.SaveChangesAsync();
        }

        return await BuildReactionsDtoAsync(postId, userId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<PostReactionsDto> BuildReactionsDtoAsync(Guid postId, Guid? currentUserId)
    {
        var reactions = await _db.PostReactions
            .AsNoTracking()
            .Where(r => r.PostId == postId)
            .ToListAsync();

        var counts = reactions
            .GroupBy(r => r.ReactionType)
            .Select(g => new ReactionCountDto(g.Key.ToString(), g.Count()))
            .ToArray();

        var totalCount = reactions.Count;

        string? currentUserReaction = null;
        if (currentUserId.HasValue)
        {
            var userReaction = reactions.FirstOrDefault(r => r.UserId == currentUserId.Value);
            currentUserReaction = userReaction?.ReactionType.ToString();
        }

        return new PostReactionsDto(counts, totalCount, currentUserReaction);
    }
}
