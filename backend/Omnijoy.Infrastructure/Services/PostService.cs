using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Core.Services;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class PostService : IPostService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMediaStorageService _storage;
    private readonly IPrivacyService _privacy;
    private readonly IFeedCache? _feedCache;
    private readonly IImageProcessingService? _imageProcessor;
    private readonly IThumbnailService? _thumbnailService;
    private readonly IMentionResolver _mentionResolver;
    private readonly INotificationService? _notifications;

    public PostService(OmnijoyDbContext db, IMediaStorageService storage, IPrivacyService privacy)
        : this(db, storage, privacy, null, null, null)
    {
    }

    public PostService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IFeedCache? feedCache)
        : this(db, storage, privacy, feedCache, null, null)
    {
    }

    public PostService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IFeedCache? feedCache,
        IImageProcessingService? imageProcessor)
        : this(db, storage, privacy, feedCache, imageProcessor, null)
    {
    }

    public PostService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IFeedCache? feedCache,
        IImageProcessingService? imageProcessor,
        IThumbnailService? thumbnailService)
        : this(
            db,
            storage,
            privacy,
            feedCache,
            imageProcessor,
            thumbnailService,
            new MentionResolver(db),
            null)
    {
    }

    public PostService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IFeedCache? feedCache,
        IImageProcessingService? imageProcessor,
        IThumbnailService? thumbnailService,
        IMentionResolver mentionResolver,
        INotificationService? notifications)
    {
        _db               = db;
        _storage          = storage;
        _privacy          = privacy;
        _feedCache        = feedCache;
        _imageProcessor   = imageProcessor;
        _thumbnailService = thumbnailService;
        _mentionResolver  = mentionResolver;
        _notifications    = notifications;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<PostDto> CreatePostAsync(
        Guid authorId,
        CreatePostRequest request,
        IReadOnlyList<MediaUploadItem>? mediaItems)
    {
        var parsedMentions = ParseMentionsOrThrow(request.Content);

        if (!Enum.TryParse<PostType>(request.PostType, ignoreCase: true, out var postType))
            throw new ArgumentException($"Invalid PostType: '{request.PostType}'.");

        if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
            throw new ArgumentException($"Invalid Privacy: '{request.Privacy}'.");

        // Followers privacy is only valid for company page posts
        if (privacy == PrivacyLevel.Followers && !request.CompanyPageId.HasValue)
            throw new ArgumentException("Followers privacy is only available for company page posts.");

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

        var mentions = await ResolveAllowedMentionsAsync(parsedMentions.Slugs, authorId);
        var mentionCreatedAt = DateTime.UtcNow;
        foreach (var mention in mentions)
        {
            _db.PostMentions.Add(new PostMention
            {
                PostId = post.Id,
                MentionedUserId = mention.UserId,
                MatchedSlug = mention.MatchedSlug,
                CreatedAt = mentionCreatedAt,
            });
        }

        // Upload + attach media files; track video entries so we can enqueue thumbnail jobs later.
        var videoMediaItems = new List<(Guid Id, string Url)>();

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

                var newMediaId = Guid.NewGuid();
                _db.PostMedia.Add(new PostMedia
                {
                    Id           = newMediaId,
                    PostId       = post.Id,
                    MediaType    = mediaType,
                    Url          = url,
                    ThumbnailUrl = null,   // populated asynchronously by ThumbnailGeneratorService
                    Order        = order++,
                });

                if (mediaType == MediaType.Video)
                    videoMediaItems.Add((newMediaId, url));
            }
        }

        await _db.SaveChangesAsync();

        await NotifyMentionsAsync(
            mentions.Where(mention => mention.UserId != authorId).Select(mention => mention.UserId),
            NotificationType.MentionInPost,
            post.Id,
            authorId);

        // Enqueue thumbnail generation for every video attachment.
        // Fire-and-forget: the queue write is fast and PostService should not wait for FFmpeg.
        if (_thumbnailService is not null)
        {
            foreach (var (mediaId, mediaUrl) in videoMediaItems)
                await _thumbnailService.EnqueueAsync(mediaId, mediaUrl);
        }

        return await LoadPostDtoAsync(post.Id, authorId)
            ?? throw new InvalidOperationException("Post not found after creation.");
    }

    // ── Feed ──────────────────────────────────────────────────────────────────

    public async Task<PagedResult<FeedItemDto>> GetFeedAsync(Guid userId, int page, int pageSize)
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
            if (cached is not null)
                return await HydrateFeedAsync(cached, userId);
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
            .Where(p => p.EventId == null)   // exclude event-wall posts from the main feed
            .Where(p => !blockedIds.Contains(p.AuthorUserId))
            .Where(p => !inactiveSet.Contains(p.AuthorUserId))
            .Where(p =>
                p.AuthorUserId == userId ||
                (friendIds.Contains(p.AuthorUserId) &&
                 (p.Privacy == PrivacyLevel.Everyone || p.Privacy == PrivacyLevel.Friends || p.Privacy == PrivacyLevel.FriendsOfFriends)) ||
                (p.Privacy == PrivacyLevel.Everyone && !friendIds.Contains(p.AuthorUserId) && p.AuthorUserId != userId) ||
                // Company page posts with Followers privacy: visible to followers of that page
                (p.CompanyPageId != null && p.Privacy == PrivacyLevel.Followers &&
                 _db.CompanyPageFollows.Any(f => f.CompanyPageId == p.CompanyPageId && f.UserId == userId))
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
                .Include(p => p.CompanyPage)
                .Include(p => p.Mentions).ThenInclude(m => m.MentionedUser)
                .Where(p => postIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id)
            : new Dictionary<Guid, Post>();

        var sharesData = shareIds.Count > 0
            ? await _db.SharedPosts.AsNoTracking()
                .Include(s => s.Sharer)
                .Include(s => s.OriginalPost).ThenInclude(p => p.Author)
                .Include(s => s.OriginalPost).ThenInclude(p => p.Media)
                .Include(s => s.OriginalPost).ThenInclude(p => p.CompanyPage)
                .Include(s => s.OriginalPost).ThenInclude(p => p.Mentions).ThenInclude(m => m.MentionedUser)
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

        var result = new PagedResult<FeedItemDto>(
            Items: items,
            Page: page,
            PageSize: pageSize,
            HasMore: (page * pageSize) < totalVisible
        );

        if (useCache)
        {
            // Cache only the viewer-neutral content snapshot. Bookmark flags
            // and author-only save totals are reloaded on every request, so a
            // stale/mis-keyed cache entry can never disclose another user's
            // private state and save/unsave does not require feed invalidation.
            await _feedCache!.SetUserFeedPage1Async(userId, result);
        }

        return await HydrateFeedAsync(result, userId);
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
                     && p.EventId == null      // exclude event-wall posts from trending
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
            .Include(p => p.CompanyPage)
            .Include(p => p.Mentions).ThenInclude(m => m.MentionedUser)
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
            .Include(p => p.CompanyPage)
            .Include(p => p.Mentions).ThenInclude(m => m.MentionedUser)
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        await EnforceReadAccessAsync(post, requesterId);

        var dto = MapToDto(post);
        return requesterId.HasValue
            ? await HydratePostAsync(dto, requesterId.Value)
            : dto;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<PostDto> UpdatePostAsync(Guid postId, Guid requesterId, UpdatePostRequest request)
    {
        var post = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Media)
            .Include(p => p.CompanyPage)
            .Include(p => p.Mentions).ThenInclude(m => m.MentionedUser)
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        if (post.AuthorUserId != requesterId)
            throw new UnauthorizedAccessException("You can only edit your own posts.");

        MentionParseResult? parsedMentions = request.Content is null
            ? null
            : ParseMentionsOrThrow(request.Content);

        IReadOnlyList<Guid> newlyMentionedUserIds = Array.Empty<Guid>();
        if (request.Content is not null)
        {
            post.Content = request.Content;
            var resolvedMentions = await ResolveAllowedMentionsAsync(parsedMentions!.Slugs, requesterId);
            newlyMentionedUserIds = SynchronizeMentions(post, resolvedMentions);
        }

        if (request.Privacy is not null)
        {
            if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
                throw new ArgumentException($"Invalid Privacy: '{request.Privacy}'.");
            post.Privacy = privacy;
        }

        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await NotifyMentionsAsync(
            newlyMentionedUserIds.Where(userId => userId != requesterId),
            NotificationType.MentionInPost,
            post.Id,
            requesterId);

        return await LoadPostDtoAsync(post.Id, requesterId)
            ?? throw new InvalidOperationException("Post not found after update.");
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

    private static MentionParseResult ParseMentionsOrThrow(string? content)
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
        Post post,
        IReadOnlyList<ResolvedMention> resolvedMentions)
    {
        var oldUserIds = post.Mentions.Select(mention => mention.MentionedUserId).ToHashSet();
        var newByUserId = resolvedMentions.ToDictionary(mention => mention.UserId);
        var now = DateTime.UtcNow;

        foreach (var existing in post.Mentions.ToArray())
        {
            if (!newByUserId.TryGetValue(existing.MentionedUserId, out var replacement))
            {
                post.Mentions.Remove(existing);
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
            post.Mentions.Add(new PostMention
            {
                PostId = post.Id,
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

    private async Task<PostDto?> LoadPostDtoAsync(Guid postId, Guid requesterId)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .Include(p => p.CompanyPage)
            .Include(p => p.Mentions).ThenInclude(m => m.MentionedUser)
            .FirstOrDefaultAsync(p => p.Id == postId);

        return post is null
            ? null
            : await HydratePostAsync(MapToDto(post), requesterId);
    }

    private async Task<PostDto> HydratePostAsync(PostDto post, Guid requesterId)
    {
        var state = await PostViewerStateHydrator.LoadAsync(
            _db,
            requesterId,
            [(post.Id, post.Author.Id)]);
        return PostViewerStateHydrator.Apply(post, requesterId, state);
    }

    private async Task<PagedResult<FeedItemDto>> HydrateFeedAsync(
        PagedResult<FeedItemDto> result,
        Guid requesterId)
    {
        var posts = result.Items
            .Select(item => item.Post ?? item.SharedPost?.OriginalPost)
            .OfType<PostDto>()
            .ToArray();

        if (posts.Length == 0)
            return result;

        var state = await PostViewerStateHydrator.LoadAsync(
            _db,
            requesterId,
            posts.Select(post => (post.Id, post.Author.Id)));

        var hydratedItems = result.Items.Select(item =>
        {
            if (item.Post is not null)
                return item with { Post = PostViewerStateHydrator.Apply(item.Post, requesterId, state) };

            if (item.SharedPost is not null)
                return item with
                {
                    SharedPost = PostViewerStateHydrator.Apply(item.SharedPost, requesterId, state),
                };

            return item;
        }).ToArray();

        return result with { Items = hydratedItems };
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

        bool canRead;
        switch (post.Privacy)
        {
            case PrivacyLevel.Everyone:
                canRead = true;
                break;

            case PrivacyLevel.OnlyMe:
                canRead = false;
                break;

            case PrivacyLevel.Friends:
            case PrivacyLevel.FriendsOfFriends:
                canRead = requesterId.HasValue &&
                    await _db.Friends.AnyAsync(f =>
                        f.Status == FriendStatus.Accepted &&
                        ((f.RequesterId == requesterId.Value && f.AddresseeId == post.AuthorUserId) ||
                         (f.RequesterId == post.AuthorUserId && f.AddresseeId == requesterId.Value)));
                break;

            case PrivacyLevel.Followers:
                // Only meaningful on company page posts; non-company Followers posts
                // are treated as private (should never occur due to CreatePostAsync guard).
                if (post.CompanyPageId.HasValue && requesterId.HasValue)
                {
                    canRead =
                        // Is a follower of the company page
                        await _db.CompanyPageFollows.AnyAsync(f =>
                            f.CompanyPageId == post.CompanyPageId.Value && f.UserId == requesterId.Value) ||
                        // Or an admin of the company page
                        await _db.CompanyPageAdmins.AnyAsync(a =>
                            a.CompanyPageId == post.CompanyPageId.Value && a.UserId == requesterId.Value);
                }
                else
                {
                    canRead = false;
                }
                break;

            default:
                canRead = false;
                break;
        }

        if (!canRead)
            throw new UnauthorizedAccessException("You do not have permission to view this post.");
    }

    internal static PostDto MapToDto(Post post)
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

        PostCompanyPageDto? companyPage = post.CompanyPage is null
            ? null
            : new PostCompanyPageDto(
                Id: post.CompanyPage.Id,
                Name: post.CompanyPage.Name,
                LogoUrl: post.CompanyPage.LogoUrl,
                UrlSlug: post.CompanyPage.UrlSlug);

        var mentions = post.Mentions
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.MatchedSlug)
            .Select(m => new MentionDto(
                MatchedSlug: m.MatchedSlug,
                UserId: m.MentionedUserId,
                DisplayName: m.MentionedUser.DisplayName,
                UrlSlug: m.MentionedUser.UrlSlug))
            .ToArray();

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
            UpdatedAt: post.UpdatedAt,
            CompanyPage: companyPage,
            Mentions: mentions
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
