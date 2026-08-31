using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>EF-backed implementation of private post bookmarks.</summary>
public class SavedPostService : ISavedPostService
{
    private readonly OmnijoyDbContext _db;
    private readonly IPrivacyService _privacy;

    public SavedPostService(OmnijoyDbContext db, IPrivacyService privacy)
    {
        _db = db;
        _privacy = privacy;
    }

    public async Task<bool> SaveAsync(Guid userId, Guid postId, Guid? collectionId = null)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        if (!post.Author.IsActive ||
            !await _privacy.CanViewPostsAsync(post.AuthorUserId, userId) ||
            !await CanViewPostAsync(post, userId))
        {
            throw new UnauthorizedAccessException("You do not have permission to save this post.");
        }

        if (collectionId.HasValue)
        {
            var ownsCollection = await _db.SavedPostCollections
                .AsNoTracking()
                .AnyAsync(c => c.Id == collectionId.Value && c.UserId == userId);
            if (!ownsCollection)
                throw new KeyNotFoundException($"Saved-post collection {collectionId.Value} not found.");
        }

        if (await IsSavedAsync(userId, postId))
            return false;

        var savedPost = new SavedPost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostId = postId,
            CollectionId = collectionId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.SavedPosts.Add(savedPost);

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request inserted (UserId, PostId) after our pre-flight
            // query. Detach the failed entry so this scoped DbContext remains
            // usable, then report the same idempotent outcome as a repeat save.
            _db.Entry(savedPost).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> UnsaveAsync(Guid userId, Guid postId)
    {
        var savedPost = await _db.SavedPosts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PostId == postId);
        if (savedPost is null)
            return false;

        _db.SavedPosts.Remove(savedPost);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsSavedAsync(Guid userId, Guid postId)
        => _db.SavedPosts
            .AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.PostId == postId);

    public async Task<HashSet<Guid>> GetSavedPostIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> postIds)
    {
        if (postIds.Count == 0)
            return [];

        var distinctIds = postIds.ToHashSet();
        var savedIds = await _db.SavedPosts
            .AsNoTracking()
            .Where(s => s.UserId == userId && distinctIds.Contains(s.PostId))
            .Select(s => s.PostId)
            .ToListAsync();

        return [.. savedIds];
    }

    public async Task<PagedResult<SavedPostDto>> GetSavedAsync(
        Guid userId,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        // Visibility is intentionally expressed as one query rather than one
        // IPrivacyService call per row. The predicates mirror PrivacyService's
        // post-level global gate plus PostService's per-post gate, including
        // blocks, default privacy settings, friendships, and page followers.
        var visibleSavedPosts = _db.SavedPosts
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Where(s => s.Post.DeletedAt == null && s.Post.Author.IsActive)
            .Where(s =>
                s.Post.AuthorUserId == userId ||
                (
                    !_db.Friends.Any(f =>
                        f.Status == FriendStatus.Blocked &&
                        ((f.RequesterId == userId && f.AddresseeId == s.Post.AuthorUserId) ||
                         (f.RequesterId == s.Post.AuthorUserId && f.AddresseeId == userId))) &&
                    (
                        (s.Post.Author.PrivacySettings != null &&
                         s.Post.Author.PrivacySettings.WhoCanSeePosts == PrivacyLevel.Everyone) ||
                        (
                            (s.Post.Author.PrivacySettings == null ||
                             s.Post.Author.PrivacySettings.WhoCanSeePosts == PrivacyLevel.Friends ||
                             s.Post.Author.PrivacySettings.WhoCanSeePosts == PrivacyLevel.FriendsOfFriends) &&
                            _db.Friends.Any(f =>
                                f.Status == FriendStatus.Accepted &&
                                ((f.RequesterId == userId && f.AddresseeId == s.Post.AuthorUserId) ||
                                 (f.RequesterId == s.Post.AuthorUserId && f.AddresseeId == userId)))
                        )
                    )
                ))
            .Where(s =>
                s.Post.AuthorUserId == userId ||
                s.Post.Privacy == PrivacyLevel.Everyone ||
                (
                    (s.Post.Privacy == PrivacyLevel.Friends ||
                     s.Post.Privacy == PrivacyLevel.FriendsOfFriends) &&
                    _db.Friends.Any(f =>
                        f.Status == FriendStatus.Accepted &&
                        ((f.RequesterId == userId && f.AddresseeId == s.Post.AuthorUserId) ||
                         (f.RequesterId == s.Post.AuthorUserId && f.AddresseeId == userId)))
                ) ||
                (
                    s.Post.Privacy == PrivacyLevel.Followers &&
                    s.Post.CompanyPageId != null &&
                    (
                        _db.CompanyPageFollows.Any(f =>
                            f.CompanyPageId == s.Post.CompanyPageId && f.UserId == userId) ||
                        _db.CompanyPageAdmins.Any(a =>
                            a.CompanyPageId == s.Post.CompanyPageId && a.UserId == userId)
                    )
                ));

        var rows = await visibleSavedPosts
            .Include(s => s.Collection)
            .Include(s => s.Post).ThenInclude(p => p.Author)
            .Include(s => s.Post).ThenInclude(p => p.Media)
            .Include(s => s.Post).ThenInclude(p => p.CompanyPage)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .AsSplitQuery()
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        var items = rows
            .Take(pageSize)
            .Select(MapToDto)
            .ToArray();

        var state = await PostViewerStateHydrator.LoadAsync(
            _db,
            userId,
            items.Select(item => (item.Post.Id, item.Post.Author.Id)));
        items = items
            .Select(item => item with
            {
                Post = PostViewerStateHydrator.Apply(item.Post, userId, state),
            })
            .ToArray();

        return new PagedResult<SavedPostDto>(items, page, pageSize, hasMore);
    }

    private async Task<bool> CanViewPostAsync(Post post, Guid userId)
    {
        if (post.AuthorUserId == userId)
            return true;

        return post.Privacy switch
        {
            PrivacyLevel.Everyone => true,
            PrivacyLevel.Friends or PrivacyLevel.FriendsOfFriends =>
                await _privacy.AreFriendsAsync(post.AuthorUserId, userId),
            PrivacyLevel.Followers when post.CompanyPageId.HasValue =>
                await _db.CompanyPageFollows.AnyAsync(f =>
                    f.CompanyPageId == post.CompanyPageId.Value && f.UserId == userId) ||
                await _db.CompanyPageAdmins.AnyAsync(a =>
                    a.CompanyPageId == post.CompanyPageId.Value && a.UserId == userId),
            _ => false,
        };
    }

    private static SavedPostDto MapToDto(SavedPost savedPost)
        => new(
            savedPost.Id,
            PostService.MapToDto(savedPost.Post),
            savedPost.Collection is null
                ? null
                : new SavedPostCollectionDto(savedPost.Collection.Id, savedPost.Collection.Name),
            savedPost.CreatedAt);

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.GetBaseException().Message;
        return message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("1062", StringComparison.OrdinalIgnoreCase);
    }
}
