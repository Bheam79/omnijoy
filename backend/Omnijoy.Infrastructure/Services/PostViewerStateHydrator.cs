using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Loads requester-specific bookmark state for a page of posts. Keeping this
/// separate from entity loading makes it possible to hydrate cached DTOs while
/// guaranteeing a bounded number of SavedPosts queries.
/// </summary>
internal static class PostViewerStateHydrator
{
    internal sealed record State(
        HashSet<Guid> SavedPostIds,
        IReadOnlyDictionary<Guid, int> AuthorSaveCounts);

    internal static async Task<State> LoadAsync(
        OmnijoyDbContext db,
        Guid requesterId,
        IEnumerable<(Guid PostId, Guid AuthorId)> posts,
        CancellationToken ct = default)
    {
        var postAuthors = posts
            .DistinctBy(p => p.PostId)
            .ToArray();

        if (postAuthors.Length == 0)
            return new State([], new Dictionary<Guid, int>());

        var postIds = postAuthors.Select(p => p.PostId).ToArray();
        var savedPostIds = await db.SavedPosts
            .AsNoTracking()
            .Where(s => s.UserId == requesterId && postIds.Contains(s.PostId))
            .Select(s => s.PostId)
            .ToListAsync(ct);

        var authoredPostIds = postAuthors
            .Where(p => p.AuthorId == requesterId)
            .Select(p => p.PostId)
            .ToArray();

        Dictionary<Guid, int> authorSaveCounts = [];
        if (authoredPostIds.Length > 0)
        {
            authorSaveCounts = await db.SavedPosts
                .AsNoTracking()
                .Where(s => authoredPostIds.Contains(s.PostId))
                .GroupBy(s => s.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, ct);
        }

        return new State([.. savedPostIds], authorSaveCounts);
    }

    internal static PostDto Apply(PostDto post, Guid requesterId, State state)
        => post with
        {
            IsSavedByMe = state.SavedPostIds.Contains(post.Id),
            SavesCount = post.Author.Id == requesterId
                ? state.AuthorSaveCounts.GetValueOrDefault(post.Id, 0)
                : null,
        };

    internal static SharedPostFeedItemDto Apply(
        SharedPostFeedItemDto sharedPost,
        Guid requesterId,
        State state)
        => sharedPost with
        {
            OriginalPost = Apply(sharedPost.OriginalPost, requesterId, state),
        };
}
