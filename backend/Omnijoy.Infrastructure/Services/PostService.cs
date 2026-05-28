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

    public PostService(OmnijoyDbContext db, IMediaStorageService storage)
    {
        _db = db;
        _storage = storage;
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

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = authorId,
            Content = request.Content ?? string.Empty,
            BackgroundImageUrl = request.BackgroundImageUrl,
            PostType = postType,
            Privacy = privacy,
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

                var url = await _storage.StoreAsync(item.Content, item.FileName, folder);

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

        // Accepted friend IDs
        var friendIds = await GetFriendIdsAsync(userId);

        // Posts visible to this user:
        //   1. Own posts (all privacy levels)
        //   2. Friend posts where Privacy is Everyone or Friends
        //   3. Non-friend posts where Privacy is Everyone
        var query = _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .Where(p => p.DeletedAt == null)
            .Where(p =>
                // Own posts
                p.AuthorUserId == userId ||
                // Friends' posts visible to friends
                (friendIds.Contains(p.AuthorUserId) &&
                 (p.Privacy == PrivacyLevel.Everyone || p.Privacy == PrivacyLevel.Friends || p.Privacy == PrivacyLevel.FriendsOfFriends)) ||
                // Public posts from anyone
                (p.Privacy == PrivacyLevel.Everyone && !friendIds.Contains(p.AuthorUserId) && p.AuthorUserId != userId)
            )
            .OrderByDescending(p => p.CreatedAt);

        var totalVisible = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToArray();

        return new FeedPageResult(
            Items: dtos,
            Page: page,
            PageSize: pageSize,
            HasMore: (page * pageSize) < totalVisible
        );
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

        return new PostDto(
            Id: post.Id,
            Author: author,
            CompanyPageId: post.CompanyPageId,
            Content: post.Content,
            BackgroundImageUrl: post.BackgroundImageUrl,
            PostType: post.PostType.ToString(),
            Privacy: post.Privacy.ToString(),
            Media: media,
            CreatedAt: post.CreatedAt,
            UpdatedAt: post.UpdatedAt
        );
    }

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
