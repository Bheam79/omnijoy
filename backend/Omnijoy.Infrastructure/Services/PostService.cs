using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class PostService : IPostService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMediaStorageService _storage;
    private readonly IPrivacyService _privacy;
    private readonly IFeedCache? _feedCache;
    private readonly IImageProcessingService? _imageProcessor;

    public PostService(OmnijoyDbContext db, IMediaStorageService storage, IPrivacyService privacy)
        : this(db, storage, privacy, null, null)
    {
    }

    public PostService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IFeedCache? feedCache)
        : this(db, storage, privacy, feedCache, null)
    {
    }

    public PostService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IFeedCache? feedCache,
        IImageProcessingService? imageProcessor)
    {
        _db             = db;
        _storage        = storage;
        _privacy        = privacy;
        _feedCache      = feedCache;
        _imageProcessor = imageProcessor;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<PostDto> CreatePostAsync(
        Guid authorId,
        CreatePostRequest request,
        IReadOnlyList<MediaUploadItem>? mediaItems)
    {
        if (!Enum.TryParse<PostType>(request.PostType, ignoreCase: true, out var postType))
            throw new ArgumentException($"Invalid PostType: '{request.PostType}'.");

        if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
            throw new ArgumentException($"Invalid Privacy: '{request.Privacy}'.");

        // Validate company page access if posting on behalf of a page
        if (request.CompanyPageId.HasValue)
        {
            var isPageAdmin = await _db.CompanyPageAdmins.AnyAsync(a =>
                a.CompanyPageId == request.CompanyPageId.Value && a.UserId == authorId);
            if (!isPageAdmin)
                throw new UnauthorizedAccessException("You are not an admin of that company page.");
        }

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = authorId,
            CompanyPageId = request.CompanyPageId,
            Content = request.Content ?? string.Empty,
            BackgroundImageUrl = request.BackgroundImageUrl,
            PostType = postType,
            Privacy = privacy,
            LinkUrl = NullIfBlank(request.LinkUrl),
            LinkTitle = NullIfBlank(request.LinkTitle),
            LinkDescription = NullIfBlank(request.LinkDescription),
            LinkImageUrl = NullIfBlank(request.LinkImageUrl),
            LinkSiteName = NullIfBlank(request.LinkSiteName),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Posts.Add(post);

        // Upload + attach media files
        if (mediaItems is { Count: > 0 })
        {
            int order = 0;
            foreach (var item in mediaItems)
            {
                var mediaType = DetermineMediaType(item.ContentType, item.FileName);
                var folder = mediaType == MediaType.Video ? "posts/videos" : "posts/images";

                string url;
                if (mediaType == MediaType.Image && _imageProcessor is not null)
                {
                    await using var processed = await _imageProcessor.ProcessImageAsync(item.Content, ImageFolder.PostImage);
                    url = await _storage.StoreAsync(processed, "image.webp", folder);
                }
                else
                {
                    url = await _storage.StoreAsync(item.Content, item.FileName, folder);
                }

                string? thumbUrl = null;
                // If a thumbnail was uploaded alongside the video (convention: fileName ends with
                // "_thumb.jpg"), treat it as the thumbnail for the preceding video entry.
                // For simplicity, we just store videos without auto-generated thumbnails here;
                // clients may pass a separate thumb file labelled "_thumb".

                _db.PostMedia.Add(new PostMedia
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    MediaType = mediaType,
                    Url = url,
                    ThumbnailUrl = thumbUrl,
                    Order = order++,
                });
            }
        }

        await _db.SaveChangesAsync();

        return await LoadPostDtoAsync(post.Id)
            ?? throw new InvalidOperationException("Post not found after creation.");
    }

    // ── Feed ──────────────────────────────────────────────────────────────────

    public async Task<FeedPageResult> GetFeedAsync(Guid userId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        // ── Cache: page 1 only, with the canonical default page size ──────────
        // We intentionally don't cache custom page sizes — keeps the key scheme
        // simple (feed:{userId}:p1) and avoids cache explosion. Callers asking
        // for non-default sizes always hit the DB.
        const int DefaultPageSize = 20;
        var useCache = _feedCache is not null && page == 1 && pageSize == DefaultPageSize;
        if (useCache)
        {
            var cached = await _feedCache!.GetUserFeedPage1Async(userId);
            if (cached is not null) return cached;
        }

        // Accepted friend IDs
        var friendIds = await GetFriendIdsAsync(userId);

        // IDs of users that have a block relationship with the requesting user
        var blockedIds = await GetBlockedUserIdsAsync(userId);

        // IDs of users whose accounts are deactivated/deleted – they should be
        // entirely invisible in the feed.
        var inactiveAuthorIds = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();
        var inactiveSet = inactiveAuthorIds.ToHashSet();

        // ── Regular posts visible to this user ────────────────────────────────
        var postQuery = _db.Posts
            .AsNoTracking()
            .Where(p => p.DeletedAt == null)
            .Where(p => !blockedIds.Contains(p.AuthorUserId))
            .Where(p => !inactiveSet.Contains(p.AuthorUserId))
            .Where(p =>
                p.AuthorUserId == userId ||
                (friendIds.Contains(p.AuthorUserId) &&
                 (p.Privacy == PrivacyLevel.Everyone || p.Privacy == PrivacyLevel.Friends || p.Privacy == PrivacyLevel.FriendsOfFriends)) ||
                (p.Privacy == PrivacyLevel.Everyone && !friendIds.Contains(p.AuthorUserId) && p.AuthorUserId != userId)
            );

        // ── Shared posts visible to this user ─────────────────────────────────
        // A share appears in the feed when:
        //   a) Sharer is the user themselves
        //   b) Sharer is a friend (OwnWall or FriendWall where target is user)
        //   c) The share targets the user's wall (TargetType == FriendWall, TargetId == userId)
        var shareQuery = _db.SharedPosts
            .AsNoTracking()
            .Where(s => !blockedIds.Contains(s.SharerId))
            .Where(s => !inactiveSet.Contains(s.SharerId))
            .Where(s =>
                s.SharerId == userId ||
                (friendIds.Contains(s.SharerId) &&
                 (s.TargetType == ShareTargetType.OwnWall ||
                  (s.TargetType == ShareTargetType.FriendWall && s.TargetId == userId))) ||
                (s.TargetType == ShareTargetType.FriendWall && s.TargetId == userId)
            );

        // ── Lightweight merge: get ID + date for all visible items ─────────────
        var postRows  = await postQuery
            .Select(p => new { p.Id, Date = p.CreatedAt, Type = "Post" })
            .ToListAsync();

        var shareRows = await shareQuery
            .Select(s => new { s.Id, Date = s.CreatedAt, Type = "SharedPost" })
            .ToListAsync();

        var allRows = postRows.Select(r => (r.Date, r.Type, r.Id))
            .Concat(shareRows.Select(r => (r.Date, r.Type, r.Id)))
            .OrderByDescending(r => r.Date)
            .ToList();

        var totalVisible = allRows.Count;
        var pageRows = allRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // ── Load full data for page items only ─────────────────────────────────
        var postIds  = pageRows.Where(r => r.Type == "Post")       .Select(r => r.Id).ToHashSet();
        var shareIds = pageRows.Where(r => r.Type == "SharedPost") .Select(r => r.Id).ToHashSet();

        var postsData = postIds.Count > 0
            ? await _db.Posts.AsNoTracking()
                .Include(p => p.Author)
                .Include(p => p.Media)
                .Where(p => postIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id)
            : new Dictionary<Guid, Post>();

        var sharesData = shareIds.Count > 0
            ? await _db.SharedPosts.AsNoTracking()
                .Include(s => s.Sharer)
                .Include(s => s.OriginalPost).ThenInclude(p => p.Author)
                .Include(s => s.OriginalPost).ThenInclude(p => p.Media)
                .Where(s => shareIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, SharedPost>();

        // ── Build FeedItemDto list in original sorted order ────────────────────
        var items = pageRows
            .Select(row =>
            {
                if (row.Type == "Post" && postsData.TryGetValue(row.Id, out var post))
                    return new FeedItemDto("Post", MapToDto(post), null);

                if (row.Type == "SharedPost" && sharesData.TryGetValue(row.Id, out var share))
                    return new FeedItemDto("SharedPost", null,
                        ShareService.MapToDto(share, share.Sharer, share.OriginalPost));

                return null;
            })
            .OfType<FeedItemDto>()
            .ToArray();

        var result = new FeedPageResult(
            Items: items,
            Page: page,
            PageSize: pageSize,
            HasMore: (page * pageSize) < totalVisible
        );

        if (useCache)
        {
            await _feedCache!.SetUserFeedPage1Async(userId, result);
        }

        return result;
    }

    // ── Trending posts ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PostDto>> GetTrendingPostsAsync(int take = 20, CancellationToken ct = default)
    {
        if (take < 1) take = 1;
        if (take > 100) take = 100;

        // Trending = public posts from the last 7 days, ordered by reaction
        // count (desc). Deleted posts and posts from deactivated authors are
        // excluded. This is per-instance (not per-user) so it's safe to cache
        // globally.
        var since = DateTime.UtcNow.AddDays(-7);

        var topPostIds = await _db.Posts
            .AsNoTracking()
            .Where(p => p.DeletedAt == null
                     && p.Privacy == PrivacyLevel.Everyone
                     && p.CreatedAt >= since
                     && p.Author.IsActive)
            .Select(p => new
            {
                p.Id,
                ReactionCount = p.Reactions.Count,
                p.CreatedAt,
            })
            .OrderByDescending(x => x.ReactionCount)
            .ThenByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (topPostIds.Count == 0) return Array.Empty<PostDto>();

        var posts = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .Where(p => topPostIds.Contains(p.Id))
            .ToListAsync(ct);

        // Preserve the ordering established by the ranking query.
        var ordered = topPostIds
            .Select(id => posts.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Select(p => MapToDto(p!))
            .ToArray();

        return ordered;
    }

    // ── Get single post ───────────────────────────────────────────────────────

    public async Task<PostDto> GetPostAsync(Guid postId, Guid? requesterId)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        await EnforceReadAccessAsync(post, requesterId);

        return MapToDto(post);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<PostDto> UpdatePostAsync(Guid postId, Guid requesterId, UpdatePostRequest request)
    {
        var post = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        if (post.AuthorUserId != requesterId)
            throw new UnauthorizedAccessException("You can only edit your own posts.");

        if (request.Content is not null)
            post.Content = request.Content;

        if (request.Privacy is not null)
        {
            if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
                throw new ArgumentException($"Invalid Privacy: '{request.Privacy}'.");
            post.Privacy = privacy;
        }

        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToDto(post);
    }

    // ── Delete (soft) ─────────────────────────────────────────────────────────

    public async Task DeletePostAsync(Guid postId, Guid requesterId)
    {
        var post = await _db.Posts
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        if (post.AuthorUserId != requesterId)
            throw new UnauthorizedAccessException("You can only delete your own posts.");

        post.DeletedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public async Task<List<Guid>> GetFriendIdsAsync(Guid userId)
    {
        return await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
    }

    /// <summary>
    /// Returns IDs of users that have a block relationship with <paramref name="userId"/>
    /// in either direction (they blocked me OR I blocked them).
    /// </summary>
    private async Task<HashSet<Guid>> GetBlockedUserIdsAsync(Guid userId)
    {
        var ids = await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Blocked &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        return [.. ids];
    }

    private async Task<PostDto?> LoadPostDtoAsync(Guid postId)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == postId);

        return post is null ? null : MapToDto(post);
    }

    private async Task EnforceReadAccessAsync(Post post, Guid? requesterId)
    {
        if (requesterId.HasValue && post.AuthorUserId == requesterId.Value)
            return; // owner always can read

        // Blocked users can never see each other's content
        if (requesterId.HasValue &&
            !await _privacy.AreNotBlockedAsync(post.AuthorUserId, requesterId.Value))
        {
            throw new UnauthorizedAccessException("You do not have permission to view this post.");
        }

        bool canRead = post.Privacy switch
        {
            PrivacyLevel.Everyone => true,
            PrivacyLevel.OnlyMe   => false,
            PrivacyLevel.Friends or PrivacyLevel.FriendsOfFriends => requesterId.HasValue &&
                await _db.Friends.AnyAsync(f =>
                    f.Status == FriendStatus.Accepted &&
                    ((f.RequesterId == requesterId.Value && f.AddresseeId == post.AuthorUserId) ||
                     (f.RequesterId == post.AuthorUserId && f.AddresseeId == requesterId.Value))),
            _ => false
        };

        if (!canRead)
            throw new UnauthorizedAccessException("You do not have permission to view this post.");
    }

    private static PostDto MapToDto(Post post)
    {
        var author = new PostAuthorDto(
            post.Author.Id,
            post.Author.DisplayName,
            post.Author.AvatarUrl
        );

        var media = post.Media
            .OrderBy(m => m.Order)
            .Select(m => new PostMediaItemDto(m.Id, m.MediaType.ToString(), m.Url, m.ThumbnailUrl, m.Order))
            .ToArray();

        PostLinkPreviewDto? linkPreview = string.IsNullOrWhiteSpace(post.LinkUrl)
            ? null
            : new PostLinkPreviewDto(
                Url: post.LinkUrl!,
                Title: post.LinkTitle,
                Description: post.LinkDescription,
                ImageUrl: post.LinkImageUrl,
                SiteName: post.LinkSiteName);

        return new PostDto(
            Id: post.Id,
            Author: author,
            CompanyPageId: post.CompanyPageId,
            Content: post.Content,
            BackgroundImageUrl: post.BackgroundImageUrl,
            PostType: post.PostType.ToString(),
            Privacy: post.Privacy.ToString(),
            Media: media,
            LinkPreview: linkPreview,
            CreatedAt: post.CreatedAt,
            UpdatedAt: post.UpdatedAt
        );
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MediaType DetermineMediaType(string contentType, string fileName)
    {
        var ct = contentType.ToLowerInvariant();
        if (ct.StartsWith("video/"))
            return MediaType.Video;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".mp4" or ".webm" or ".mov" or ".avi" or ".mkv")
            return MediaType.Video;

        return MediaType.Image;
    }
}
