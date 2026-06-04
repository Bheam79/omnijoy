using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class CompanyPageService : ICompanyPageService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMediaStorageService _storage;
    private readonly IImageProcessingService _imageProcessor;

    public CompanyPageService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IImageProcessingService imageProcessor)
    {
        _db             = db;
        _storage        = storage;
        _imageProcessor = imageProcessor;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<CompanyPageDto> CreatePageAsync(
        Guid creatorId,
        CreateCompanyPageRequest request,
        PageImageUploadItem? logo,
        PageImageUploadItem? cover)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");

        string? logoUrl  = null;
        string? coverUrl = null;

        if (logo is not null)
        {
            await using var processedLogo = await _imageProcessor.ProcessImageAsync(logo.Content, ImageFolder.Avatar);
            logoUrl = await _storage.StoreAsync(processedLogo, "logo.webp", "company/logos");
        }
        if (cover is not null)
        {
            await using var processedCover = await _imageProcessor.ProcessImageAsync(cover.Content, ImageFolder.Cover);
            coverUrl = await _storage.StoreAsync(processedCover, "cover.webp", "company/covers");
        }

        var page = new CompanyPage
        {
            Id               = Guid.NewGuid(),
            Name             = request.Name.Trim(),
            Description      = request.Description?.Trim(),
            LogoUrl          = logoUrl,
            CoverUrl         = coverUrl,
            CreatedByUserId  = creatorId,
            CreatedAt        = DateTime.UtcNow,
            AddressPlaceId   = request.AddressPlaceId,
            AddressText      = request.AddressText?.Trim(),
            AddressCity      = request.AddressCity?.Trim(),
            AddressCountry   = request.AddressCountry?.Trim(),
            AddressLatitude  = request.AddressLatitude,
            AddressLongitude = request.AddressLongitude,
        };

        _db.CompanyPages.Add(page);

        // Creator is automatically Owner
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId        = creatorId,
            Role          = CompanyPageRole.Owner,
        });

        await _db.SaveChangesAsync();

        return await LoadPageDtoAsync(page.Id, creatorId)
            ?? throw new InvalidOperationException("Page not found after creation.");
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<PagedResult<CompanyPageDto>> GetPagesAsync(
        Guid requesterId,
        bool mineOnly,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        IQueryable<CompanyPage> query = _db.CompanyPages
            .AsNoTracking()
            .Include(cp => cp.CreatedByUser)
            .Include(cp => cp.Admins)
            .Include(cp => cp.Followers);

        if (mineOnly)
        {
            query = query.Where(cp =>
                cp.Admins.Any(a => a.UserId == requesterId));
        }

        query = query.OrderBy(cp => cp.Name);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(cp => MapToDto(cp, requesterId)).ToArray();

        return new PagedResult<CompanyPageDto>(dtos, page, pageSize, (page * pageSize) < total);
    }

    // ── Get single ────────────────────────────────────────────────────────────

    public async Task<CompanyPageDto> GetPageAsync(Guid pageId, Guid? requesterId)
    {
        return await LoadPageDtoAsync(pageId, requesterId)
            ?? throw new KeyNotFoundException($"Company page {pageId} not found.");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<CompanyPageDto> UpdatePageAsync(
        Guid pageId,
        Guid requesterId,
        UpdateCompanyPageRequest request,
        PageImageUploadItem? logo,
        PageImageUploadItem? cover)
    {
        var cp = await _db.CompanyPages
            .Include(c => c.Admins)
            .Include(c => c.Followers)
            .Include(c => c.CreatedByUser)
            .FirstOrDefaultAsync(c => c.Id == pageId)
            ?? throw new KeyNotFoundException($"Company page {pageId} not found.");

        var requesterAdmin = cp.Admins.FirstOrDefault(a => a.UserId == requesterId);
        if (requesterAdmin is null || requesterAdmin.Role == CompanyPageRole.Editor)
            throw new UnauthorizedAccessException("Only Owners and Admins can edit a company page.");

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Name cannot be empty.");
            cp.Name = request.Name.Trim();
        }

        if (request.Description is not null)
            cp.Description = request.Description.Trim();

        if (request.AddressPlaceId is not null)
            cp.AddressPlaceId = string.IsNullOrWhiteSpace(request.AddressPlaceId) ? null : request.AddressPlaceId;

        if (request.AddressText is not null)
            cp.AddressText = string.IsNullOrWhiteSpace(request.AddressText) ? null : request.AddressText.Trim();

        if (request.AddressCity is not null)
            cp.AddressCity = string.IsNullOrWhiteSpace(request.AddressCity) ? null : request.AddressCity.Trim();

        if (request.AddressCountry is not null)
            cp.AddressCountry = string.IsNullOrWhiteSpace(request.AddressCountry) ? null : request.AddressCountry.Trim();

        if (request.AddressLatitude is not null)
            cp.AddressLatitude = request.AddressLatitude;

        if (request.AddressLongitude is not null)
            cp.AddressLongitude = request.AddressLongitude;

        if (logo is not null)
        {
            await using var processedLogo = await _imageProcessor.ProcessImageAsync(logo.Content, ImageFolder.Avatar);
            cp.LogoUrl = await _storage.StoreAsync(processedLogo, "logo.webp", "company/logos");
        }
        if (cover is not null)
        {
            await using var processedCover = await _imageProcessor.ProcessImageAsync(cover.Content, ImageFolder.Cover);
            cp.CoverUrl = await _storage.StoreAsync(processedCover, "cover.webp", "company/covers");
        }

        await _db.SaveChangesAsync();

        return MapToDto(cp, requesterId);
    }

    // ── Admins ────────────────────────────────────────────────────────────────

    public async Task<AdminsResult> GetAdminsAsync(Guid pageId, Guid? requesterId)
    {
        var exists = await _db.CompanyPages.AnyAsync(cp => cp.Id == pageId);
        if (!exists)
            throw new KeyNotFoundException($"Company page {pageId} not found.");

        var admins = await _db.CompanyPageAdmins
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.CompanyPageId == pageId)
            .ToListAsync();

        return new AdminsResult(admins.Select(ToAdminDto).ToArray());
    }

    public async Task<AdminsResult> AddOrUpdateAdminAsync(
        Guid pageId,
        Guid requesterId,
        AddAdminRequest request)
    {
        var cp = await _db.CompanyPages
            .Include(c => c.Admins).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Id == pageId)
            ?? throw new KeyNotFoundException($"Company page {pageId} not found.");

        var requesterRole = cp.Admins.FirstOrDefault(a => a.UserId == requesterId)?.Role;
        if (requesterRole is null)
            throw new UnauthorizedAccessException("You are not an admin of this page.");

        if (!Enum.TryParse<CompanyPageRole>(request.Role, ignoreCase: true, out var newRole))
            throw new ArgumentException($"Invalid role: '{request.Role}'. Use Admin or Editor.");

        // Editors cannot promote anyone
        if (requesterRole == CompanyPageRole.Editor)
            throw new UnauthorizedAccessException("Editors cannot manage admins.");

        // Only Owner can assign Owner or Admin
        if (newRole == CompanyPageRole.Owner && requesterRole != CompanyPageRole.Owner)
            throw new UnauthorizedAccessException("Only Owners can assign the Owner role.");

        // Cannot demote the only Owner
        if (request.UserId == requesterId && requesterRole == CompanyPageRole.Owner)
        {
            var ownerCount = cp.Admins.Count(a => a.Role == CompanyPageRole.Owner);
            if (ownerCount == 1 && newRole != CompanyPageRole.Owner)
                throw new InvalidOperationException("Cannot demote the last Owner. Assign another Owner first.");
        }

        var target = cp.Admins.FirstOrDefault(a => a.UserId == request.UserId);
        if (target is null)
        {
            var newAdmin = new CompanyPageAdmin
            {
                CompanyPageId = pageId,
                UserId        = request.UserId,
                Role          = newRole,
            };
            _db.CompanyPageAdmins.Add(newAdmin);
        }
        else
        {
            target.Role = newRole;
        }

        await _db.SaveChangesAsync();

        // Reload to get navigation props for new admin
        return await GetAdminsAsync(pageId, requesterId);
    }

    public async Task<AdminsResult> RemoveAdminAsync(
        Guid pageId,
        Guid requesterId,
        Guid targetUserId)
    {
        var cp = await _db.CompanyPages
            .Include(c => c.Admins)
            .FirstOrDefaultAsync(c => c.Id == pageId)
            ?? throw new KeyNotFoundException($"Company page {pageId} not found.");

        var requesterRole = cp.Admins.FirstOrDefault(a => a.UserId == requesterId)?.Role;
        if (requesterRole is null)
            throw new UnauthorizedAccessException("You are not an admin of this page.");

        var target = cp.Admins.FirstOrDefault(a => a.UserId == targetUserId)
            ?? throw new KeyNotFoundException("User is not an admin of this page.");

        // Editors cannot remove anyone
        if (requesterRole == CompanyPageRole.Editor)
            throw new UnauthorizedAccessException("Editors cannot manage admins.");

        // Admins can only remove Editors
        if (requesterRole == CompanyPageRole.Admin && target.Role != CompanyPageRole.Editor)
            throw new UnauthorizedAccessException("Admins can only remove Editors.");

        // Cannot remove the last Owner
        if (target.Role == CompanyPageRole.Owner)
        {
            var ownerCount = cp.Admins.Count(a => a.Role == CompanyPageRole.Owner);
            if (ownerCount <= 1)
                throw new InvalidOperationException("Cannot remove the last Owner.");
        }

        _db.CompanyPageAdmins.Remove(target);
        await _db.SaveChangesAsync();

        return await GetAdminsAsync(pageId, requesterId);
    }

    // ── Follow / Unfollow ─────────────────────────────────────────────────────

    public async Task<CompanyPageDto> FollowAsync(Guid pageId, Guid userId)
    {
        var exists = await _db.CompanyPages.AnyAsync(cp => cp.Id == pageId);
        if (!exists)
            throw new KeyNotFoundException($"Company page {pageId} not found.");

        var already = await _db.CompanyPageFollows
            .AnyAsync(f => f.CompanyPageId == pageId && f.UserId == userId);

        if (!already)
        {
            _db.CompanyPageFollows.Add(new CompanyPageFollow
            {
                CompanyPageId = pageId,
                UserId        = userId,
                CreatedAt     = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
        }

        return await LoadPageDtoAsync(pageId, userId)
            ?? throw new InvalidOperationException("Page not found after follow.");
    }

    public async Task<CompanyPageDto> UnfollowAsync(Guid pageId, Guid userId)
    {
        var follow = await _db.CompanyPageFollows
            .FirstOrDefaultAsync(f => f.CompanyPageId == pageId && f.UserId == userId);

        if (follow is not null)
        {
            _db.CompanyPageFollows.Remove(follow);
            await _db.SaveChangesAsync();
        }

        return await LoadPageDtoAsync(pageId, userId)
            ?? throw new KeyNotFoundException($"Company page {pageId} not found.");
    }

    // ── My admin pages (for post composer) ───────────────────────────────────

    public async Task<CompanyPageDto[]> GetMyAdminPagesAsync(Guid userId)
    {
        var pages = await _db.CompanyPages
            .AsNoTracking()
            .Include(cp => cp.CreatedByUser)
            .Include(cp => cp.Admins)
            .Include(cp => cp.Followers)
            .Where(cp => cp.Admins.Any(a => a.UserId == userId))
            .OrderBy(cp => cp.Name)
            .ToListAsync();

        return pages.Select(cp => MapToDto(cp, userId)).ToArray();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<CompanyPageDto?> LoadPageDtoAsync(Guid pageId, Guid? requesterId)
    {
        var cp = await _db.CompanyPages
            .AsNoTracking()
            .Include(c => c.CreatedByUser)
            .Include(c => c.Admins)
            .Include(c => c.Followers)
            .FirstOrDefaultAsync(c => c.Id == pageId);

        return cp is null ? null : MapToDto(cp, requesterId);
    }

    private static CompanyPageDto MapToDto(CompanyPage cp, Guid? requesterId)
    {
        var creator = new CompanyPageCreatorDto(
            cp.CreatedByUser.Id,
            cp.CreatedByUser.DisplayName,
            cp.CreatedByUser.AvatarUrl
        );

        int followerCount = cp.Followers.Count;
        bool isFollowing  = requesterId.HasValue && cp.Followers.Any(f => f.UserId == requesterId.Value);
        string? myRole    = requesterId.HasValue
            ? cp.Admins.FirstOrDefault(a => a.UserId == requesterId.Value)?.Role.ToString()
            : null;

        return new CompanyPageDto(
            Id:               cp.Id,
            Name:             cp.Name,
            Description:      cp.Description,
            LogoUrl:          cp.LogoUrl,
            CoverUrl:         cp.CoverUrl,
            CreatedBy:        creator,
            FollowerCount:    followerCount,
            IsFollowing:      isFollowing,
            MyRole:           myRole,
            CreatedAt:        cp.CreatedAt,
            UrlSlug:          cp.UrlSlug,
            AddressText:      cp.AddressText,
            AddressCity:      cp.AddressCity,
            AddressCountry:   cp.AddressCountry,
            AddressLatitude:  cp.AddressLatitude,
            AddressLongitude: cp.AddressLongitude
        );
    }

    private static CompanyPageAdminDto ToAdminDto(CompanyPageAdmin a) =>
        new(a.UserId, a.User.DisplayName, a.User.AvatarUrl, a.Role.ToString());
}
