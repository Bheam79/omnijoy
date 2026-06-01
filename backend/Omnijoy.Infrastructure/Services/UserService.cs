using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMediaStorageService _storage;
    private readonly IPrivacyService _privacy;
    private readonly IImageProcessingService _imageProcessor;

    public UserService(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        IPrivacyService privacy,
        IImageProcessingService imageProcessor)
    {
        _db             = db;
        _storage        = storage;
        _privacy        = privacy;
        _imageProcessor = imageProcessor;
    }

    // ── GET profile ───────────────────────────────────────────────────────────

    public async Task<UserProfileDto> GetUserProfileAsync(Guid userId, Guid? requesterId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var isOwn = requesterId.HasValue && requesterId.Value == userId;

        // Determine friendship
        bool isFriend = false;
        if (!isOwn && requesterId.HasValue)
        {
            isFriend = await _privacy.AreFriendsAsync(userId, requesterId.Value);
        }

        // Enforce profile visibility via the central PrivacyService
        // (this also handles the block check)
        if (!isOwn)
        {
            bool canView = await _privacy.CanViewProfileAsync(userId, requesterId);
            if (!canView)
                throw new UnauthorizedAccessException("This profile is private.");
        }

        // Friend count
        var friendCount = await _db.Friends.CountAsync(f =>
            f.Status == FriendStatus.Accepted &&
            (f.RequesterId == userId || f.AddresseeId == userId));

        // Mutual friends (only meaningful when viewing another user's profile)
        int? mutualCount = null;
        if (!isOwn && requesterId.HasValue)
        {
            var myFriendIds = await _db.Friends
                .AsNoTracking()
                .Where(f => f.Status == FriendStatus.Accepted &&
                            (f.RequesterId == requesterId.Value || f.AddresseeId == requesterId.Value))
                .Select(f => f.RequesterId == requesterId.Value ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            var theirFriendIds = await _db.Friends
                .AsNoTracking()
                .Where(f => f.Status == FriendStatus.Accepted &&
                            (f.RequesterId == userId || f.AddresseeId == userId))
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            mutualCount = myFriendIds.Intersect(theirFriendIds).Count();
        }

        // Birth date: only expose when ShowBirthDate is true, or owner is viewing
        string? birthDate = (isOwn || user.ShowBirthDate)
            ? user.BirthDate?.ToString("yyyy-MM-dd")
            : null;

        return new UserProfileDto(
            Id: user.Id,
            DisplayName: user.DisplayName,
            AvatarUrl: user.AvatarUrl,
            CoverUrl: user.CoverUrl,
            Bio: user.Bio,
            Gender: user.Gender.ToString(),
            BirthDate: birthDate,
            ShowBirthDate: user.ShowBirthDate,
            FriendCount: friendCount,
            MutualFriendCount: mutualCount,
            IsOwnProfile: isOwn,
            IsFriend: isFriend,
            CreatedAt: user.CreatedAt,
            UrlSlug: user.UrlSlug,
            LocationName: user.LocationName,
            LocationCity: user.LocationCity,
            LocationCountry: user.LocationCountry,
            LocationCountryCode: user.LocationCountryCode,
            LocationLatitude: user.LocationLatitude,
            LocationLongitude: user.LocationLongitude
        );
    }

    // ── UPDATE profile ────────────────────────────────────────────────────────

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (request.DisplayName is not null)
        {
            var trimmed = request.DisplayName.Trim();
            if (trimmed.Length < 1 || trimmed.Length > 128)
                throw new ArgumentException("Display name must be between 1 and 128 characters.");
            user.DisplayName = trimmed;
        }

        if (request.Bio is not null)
        {
            if (request.Bio.Length > 2000)
                throw new ArgumentException("Bio must be 2000 characters or fewer.");
            user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        }

        if (request.Gender is not null)
        {
            if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
                throw new ArgumentException($"Invalid gender value '{request.Gender}'. Allowed: Male, Female, NotDisclosed.");
            user.Gender = gender;
        }

        // BirthDate: "yyyy-MM-dd" or empty string to clear
        if (request.BirthDate is not null)
        {
            if (string.IsNullOrWhiteSpace(request.BirthDate))
            {
                user.BirthDate = null;
            }
            else if (DateOnly.TryParseExact(request.BirthDate, "yyyy-MM-dd", out var dob))
            {
                user.BirthDate = dob;
            }
            else
            {
                throw new ArgumentException("BirthDate must be in 'yyyy-MM-dd' format.");
            }
        }

        if (request.ShowBirthDate is not null)
            user.ShowBirthDate = request.ShowBirthDate.Value;

        // Location fields — all optional; pass null to leave unchanged
        if (request.LocationPlaceId is not null)
            user.LocationPlaceId = string.IsNullOrWhiteSpace(request.LocationPlaceId) ? null : request.LocationPlaceId;

        if (request.LocationName is not null)
            user.LocationName = string.IsNullOrWhiteSpace(request.LocationName) ? null : request.LocationName.Trim();

        if (request.LocationCity is not null)
            user.LocationCity = string.IsNullOrWhiteSpace(request.LocationCity) ? null : request.LocationCity.Trim();

        if (request.LocationCountry is not null)
            user.LocationCountry = string.IsNullOrWhiteSpace(request.LocationCountry) ? null : request.LocationCountry.Trim();

        if (request.LocationCountryCode is not null)
            user.LocationCountryCode = string.IsNullOrWhiteSpace(request.LocationCountryCode) ? null : request.LocationCountryCode.Trim();

        if (request.LocationLatitude is not null)
            user.LocationLatitude = request.LocationLatitude;

        if (request.LocationLongitude is not null)
            user.LocationLongitude = request.LocationLongitude;

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapUserDto(user);
    }

    // ── AVATAR upload ─────────────────────────────────────────────────────────

    public async Task<UserDto> UploadAvatarAsync(Guid userId, Stream fileStream, string fileName)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        // Delete old avatar (best-effort — local storage only deletes /uploads/ paths)
        if (user.AvatarUrl is not null)
            await _storage.DeleteAsync(user.AvatarUrl);

        await using var processed = await _imageProcessor.ProcessImageAsync(fileStream, ImageFolder.Avatar);
        user.AvatarUrl = await _storage.StoreAsync(processed, "avatar.webp", "avatars");
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapUserDto(user);
    }

    // ── COVER upload ──────────────────────────────────────────────────────────

    public async Task<UserDto> UploadCoverAsync(Guid userId, Stream fileStream, string fileName)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.CoverUrl is not null)
            await _storage.DeleteAsync(user.CoverUrl);

        await using var processed = await _imageProcessor.ProcessImageAsync(fileStream, ImageFolder.Cover);
        user.CoverUrl = await _storage.StoreAsync(processed, "cover.webp", "covers");
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapUserDto(user);
    }

    // ── PRIVACY settings ──────────────────────────────────────────────────────

    public async Task<PrivacySettingsDto> GetPrivacySettingsAsync(Guid userId)
    {
        var settings = await _db.UserPrivacySettings.FindAsync(userId);

        if (settings is null)
        {
            // Auto-create defaults (shouldn't happen for registered users, but defensive)
            settings = new UserPrivacySettings { UserId = userId };
            _db.UserPrivacySettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        return MapPrivacyDto(settings);
    }

    public async Task<PrivacySettingsDto> UpdatePrivacySettingsAsync(Guid userId, UpdatePrivacyRequest request)
    {
        var settings = await _db.UserPrivacySettings.FindAsync(userId);
        if (settings is null)
        {
            settings = new UserPrivacySettings { UserId = userId };
            _db.UserPrivacySettings.Add(settings);
        }

        if (request.WhoCanSeePosts is not null)
            settings.WhoCanSeePosts = ParsePrivacy(request.WhoCanSeePosts, nameof(request.WhoCanSeePosts));

        if (request.WhoCanSeeProfile is not null)
            settings.WhoCanSeeProfile = ParsePrivacy(request.WhoCanSeeProfile, nameof(request.WhoCanSeeProfile));

        if (request.WhoCanSendMessages is not null)
            settings.WhoCanSendMessages = ParsePrivacy(request.WhoCanSendMessages, nameof(request.WhoCanSendMessages));

        if (request.WhoCanSeeFriendList is not null)
            settings.WhoCanSeeFriendList = ParsePrivacy(request.WhoCanSeeFriendList, nameof(request.WhoCanSeeFriendList));

        if (request.WhoCanSeeEvents is not null)
            settings.WhoCanSeeEvents = ParsePrivacy(request.WhoCanSeeEvents, nameof(request.WhoCanSeeEvents));

        if (request.WhoCanTagInPosts is not null)
            settings.WhoCanTagInPosts = ParsePrivacy(request.WhoCanTagInPosts, nameof(request.WhoCanTagInPosts));

        await _db.SaveChangesAsync();
        return MapPrivacyDto(settings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PrivacyLevel ParsePrivacy(string value, string fieldName)
    {
        if (!Enum.TryParse<PrivacyLevel>(value, ignoreCase: true, out var level))
            throw new ArgumentException(
                $"Invalid value '{value}' for {fieldName}. " +
                "Allowed: Everyone, FriendsOfFriends, Friends, OnlyMe.");
        return level;
    }

    internal static UserDto MapUserDto(User u) => new(
        Id: u.Id,
        Email: u.Email,
        DisplayName: u.DisplayName,
        AvatarUrl: u.AvatarUrl,
        CoverUrl: u.CoverUrl,
        Bio: u.Bio,
        Gender: u.Gender.ToString(),
        BirthDate: u.BirthDate?.ToString("yyyy-MM-dd"),
        ShowBirthDate: u.ShowBirthDate,
        CreatedAt: u.CreatedAt,
        UrlSlug: u.UrlSlug,
        Role: u.Role.ToString(),
        LocationName: u.LocationName,
        LocationCity: u.LocationCity,
        LocationCountry: u.LocationCountry
    );

    private static PrivacySettingsDto MapPrivacyDto(UserPrivacySettings s) => new(
        WhoCanSeePosts: s.WhoCanSeePosts.ToString(),
        WhoCanSeeProfile: s.WhoCanSeeProfile.ToString(),
        WhoCanSendMessages: s.WhoCanSendMessages.ToString(),
        WhoCanSeeFriendList: s.WhoCanSeeFriendList.ToString(),
        WhoCanSeeEvents: s.WhoCanSeeEvents.ToString(),
        WhoCanTagInPosts: s.WhoCanTagInPosts.ToString()
    );
}
