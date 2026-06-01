using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class ShareService : IShareService
{
    private readonly OmnijoyDbContext _db;

    public ShareService(OmnijoyDbContext db)
    {
        _db = db;
    }

    // ── Create share ──────────────────────────────────────────────────────────

    public async Task<SharedPostFeedItemDto> CreateShareAsync(
        Guid sharerId,
        Guid postId,
        CreateShareRequest request)
    {
        // Parse and validate TargetType
        if (!Enum.TryParse<ShareTargetType>(request.TargetType, ignoreCase: true, out var targetType))
            throw new ArgumentException($"Invalid TargetType: '{request.TargetType}'.");

        // Validate TargetId requirements
        if (targetType is ShareTargetType.FriendWall or ShareTargetType.CompanyPage &&
            !request.TargetId.HasValue)
        {
            throw new ArgumentException($"TargetId is required for TargetType '{targetType}'.");
        }

        // Load original post (must exist and not be deleted)
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .Include(p => p.CompanyPage)
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Post {postId} not found.");

        // Post must be visible to the sharer (OnlyMe posts cannot be shared by others)
        if (post.Privacy == PrivacyLevel.OnlyMe && post.AuthorUserId != sharerId)
            throw new UnauthorizedAccessException("You do not have permission to share this post.");

        // For FriendWall: the target must be an accepted friend of the sharer
        if (targetType == ShareTargetType.FriendWall)
        {
            var isFriend = await _db.Friends.AnyAsync(f =>
                f.Status == FriendStatus.Accepted &&
                ((f.RequesterId == sharerId && f.AddresseeId == request.TargetId!.Value) ||
                 (f.RequesterId == request.TargetId!.Value && f.AddresseeId == sharerId)));

            if (!isFriend)
                throw new UnauthorizedAccessException("You can only share to a friend's wall.");
        }

        // For CompanyPage: the sharer must follow or admin the page
        if (targetType == ShareTargetType.CompanyPage)
        {
            var hasAccess = await _db.CompanyPageAdmins.AnyAsync(a =>
                                a.CompanyPageId == request.TargetId!.Value && a.UserId == sharerId) ||
                            await _db.CompanyPageFollows.AnyAsync(f =>
                                f.CompanyPageId == request.TargetId!.Value && f.UserId == sharerId);

            if (!hasAccess)
                throw new UnauthorizedAccessException("You must follow the company page to share to it.");
        }

        var share = new SharedPost
        {
            Id              = Guid.NewGuid(),
            OriginalPostId  = postId,
            SharerId        = sharerId,
            TargetType      = targetType,
            TargetId        = request.TargetId,
            OptionalMessage = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            CreatedAt       = DateTime.UtcNow,
        };

        _db.SharedPosts.Add(share);
        await _db.SaveChangesAsync();

        // Load sharer user for DTO
        var sharer = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == sharerId);

        return MapToDto(share, sharer, post);
    }

    // ── Recipient IDs for feed / notifications ─────────────────────────────────

    public async Task<List<Guid>> GetShareRecipientIdsAsync(Guid sharerId, SharedPostFeedItemDto share)
    {
        var recipients = new HashSet<Guid> { sharerId };

        if (share.TargetType == "FriendWall" && share.TargetId.HasValue)
        {
            // Notify the target user and their friends
            recipients.Add(share.TargetId.Value);

            var targetFriends = await GetFriendIdsAsync(share.TargetId.Value);
            foreach (var id in targetFriends) recipients.Add(id);
        }
        else if (share.TargetType == "CompanyPage" && share.TargetId.HasValue)
        {
            // Notify all followers of the page
            var followers = await _db.CompanyPageFollows
                .AsNoTracking()
                .Where(f => f.CompanyPageId == share.TargetId.Value)
                .Select(f => f.UserId)
                .ToListAsync();

            foreach (var id in followers) recipients.Add(id);
        }
        else
        {
            // OwnWall: sharer's friends
            var sharerFriends = await GetFriendIdsAsync(sharerId);
            foreach (var id in sharerFriends) recipients.Add(id);
        }

        return [.. recipients];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<Guid>> GetFriendIdsAsync(Guid userId)
    {
        return await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
    }

    internal static SharedPostFeedItemDto MapToDto(SharedPost share, User sharer, Post originalPost)
    {
        var sharerDto = new PostAuthorDto(sharer.Id, sharer.DisplayName, sharer.AvatarUrl);

        var media = originalPost.Media
            .OrderBy(m => m.Order)
            .Select(m => new PostMediaItemDto(m.Id, m.MediaType.ToString(), m.Url, m.ThumbnailUrl, m.Order))
            .ToArray();

        PostLinkPreviewDto? linkPreview = string.IsNullOrWhiteSpace(originalPost.LinkUrl)
            ? null
            : new PostLinkPreviewDto(
                Url: originalPost.LinkUrl!,
                Title: originalPost.LinkTitle,
                Description: originalPost.LinkDescription,
                ImageUrl: originalPost.LinkImageUrl,
                SiteName: originalPost.LinkSiteName);

        var originalAuthorDto = new PostAuthorDto(
            originalPost.Author.Id,
            originalPost.Author.DisplayName,
            originalPost.Author.AvatarUrl);

        PostCompanyPageDto? originalCompanyPage = originalPost.CompanyPage is null
            ? null
            : new PostCompanyPageDto(
                Id: originalPost.CompanyPage.Id,
                Name: originalPost.CompanyPage.Name,
                LogoUrl: originalPost.CompanyPage.LogoUrl,
                UrlSlug: originalPost.CompanyPage.UrlSlug);

        var originalPostDto = new PostDto(
            Id: originalPost.Id,
            Author: originalAuthorDto,
            CompanyPageId: originalPost.CompanyPageId,
            Content: originalPost.Content,
            BackgroundImageUrl: originalPost.BackgroundImageUrl,
            PostType: originalPost.PostType.ToString(),
            Privacy: originalPost.Privacy.ToString(),
            Media: media,
            LinkPreview: linkPreview,
            CreatedAt: originalPost.CreatedAt,
            UpdatedAt: originalPost.UpdatedAt,
            CompanyPage: originalCompanyPage);

        return new SharedPostFeedItemDto(
            Id:           share.Id,
            Sharer:       sharerDto,
            Message:      share.OptionalMessage,
            TargetType:   share.TargetType.ToString(),
            TargetId:     share.TargetId,
            OriginalPost: originalPostDto,
            CreatedAt:    share.CreatedAt);
    }
}
