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

    // ── Who reacted ───────────────────────────────────────────────────────────

    public async Task<ReactionWhoDto> GetReactionWhoAsync(Guid postId, Guid? currentUserId)
    {
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Post {postId} not found.");

        // Fetch all reactors with their display names and avatars.
        var reactors = await _db.PostReactions
            .AsNoTracking()
            .Where(r => r.PostId == postId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.UserId,
                r.User.DisplayName,
                r.User.AvatarUrl,
                ReactionType = r.ReactionType.ToString(),
            })
            .ToListAsync();

        int total = reactors.Count;
        const int maxListed = 5;

        if (total == 0)
            return new ReactionWhoDto([], 0);

        // Determine which of these users are friends of the current user.
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

        // Sort: friends first, then rest — preserving CreatedAt order within each group.
        var sorted = reactors
            .OrderByDescending(r => friendIds.Contains(r.UserId))
            .ThenBy(_ => 0)  // preserve stable relative order from the original OrderBy
            .ToList();

        var listed = sorted.Take(maxListed).ToArray();
        int remaining = total - listed.Length;

        var people = listed.Select(r => new ReactionWhoUserDto(
            r.UserId,
            r.DisplayName,
            r.AvatarUrl,
            friendIds.Contains(r.UserId),
            r.ReactionType
        )).ToArray();

        return new ReactionWhoDto(people, remaining);
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
