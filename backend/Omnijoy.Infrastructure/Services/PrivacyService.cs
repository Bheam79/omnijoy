using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Centralised privacy gate.  All other services should delegate access checks
/// here rather than duplicating friend-lookup and block-check logic.
/// </summary>
public class PrivacyService : IPrivacyService
{
    private readonly OmnijoyDbContext _db;

    public PrivacyService(OmnijoyDbContext db) => _db = db;

    // ── Block check ───────────────────────────────────────────────────────────

    public async Task<bool> AreNotBlockedAsync(Guid userA, Guid userB)
    {
        // True when NO block record exists in either direction
        return !await _db.Friends.AnyAsync(f =>
            f.Status == FriendStatus.Blocked &&
            ((f.RequesterId == userA && f.AddresseeId == userB) ||
             (f.RequesterId == userB && f.AddresseeId == userA)));
    }

    // ── Friendship check ──────────────────────────────────────────────────────

    public async Task<bool> AreFriendsAsync(Guid userA, Guid userB)
    {
        return await _db.Friends.AnyAsync(f =>
            f.Status == FriendStatus.Accepted &&
            ((f.RequesterId == userA && f.AddresseeId == userB) ||
             (f.RequesterId == userB && f.AddresseeId == userA)));
    }

    // ── Profile visibility ────────────────────────────────────────────────────

    public async Task<bool> CanViewProfileAsync(Guid targetUserId, Guid? requesterId)
    {
        if (requesterId.HasValue && requesterId.Value == targetUserId)
            return true; // owner always sees their own profile

        if (requesterId.HasValue && !await AreNotBlockedAsync(targetUserId, requesterId.Value))
            return false; // blocked users can never see each other

        var settings = await GetSettingsAsync(targetUserId);
        return await CheckLevelAsync(settings.WhoCanSeeProfile, targetUserId, requesterId);
    }

    // ── Post visibility (global setting) ─────────────────────────────────────

    public async Task<bool> CanViewPostsAsync(Guid targetUserId, Guid? requesterId)
    {
        if (requesterId.HasValue && requesterId.Value == targetUserId)
            return true;

        if (requesterId.HasValue && !await AreNotBlockedAsync(targetUserId, requesterId.Value))
            return false;

        var settings = await GetSettingsAsync(targetUserId);
        return await CheckLevelAsync(settings.WhoCanSeePosts, targetUserId, requesterId);
    }

    // ── Message initiation ────────────────────────────────────────────────────

    public async Task<bool> CanSendMessagesAsync(Guid targetUserId, Guid requesterId)
    {
        if (targetUserId == requesterId)
            return false; // cannot message yourself

        if (!await AreNotBlockedAsync(targetUserId, requesterId))
            return false;

        var settings = await GetSettingsAsync(targetUserId);

        return settings.WhoCanSendMessages switch
        {
            PrivacyLevel.Everyone         => true,
            PrivacyLevel.FriendsOfFriends => await AreFriendsAsync(targetUserId, requesterId),
            PrivacyLevel.Friends          => await AreFriendsAsync(targetUserId, requesterId),
            PrivacyLevel.OnlyMe           => false, // nobody can initiate
            _                             => false,
        };
    }

    // ── Friend-list visibility ────────────────────────────────────────────────

    public async Task<bool> CanViewFriendListAsync(Guid targetUserId, Guid? requesterId)
    {
        if (requesterId.HasValue && requesterId.Value == targetUserId)
            return true;

        if (requesterId.HasValue && !await AreNotBlockedAsync(targetUserId, requesterId.Value))
            return false;

        var settings = await GetSettingsAsync(targetUserId);
        return await CheckLevelAsync(settings.WhoCanSeeFriendList, targetUserId, requesterId);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Loads the user's privacy settings, falling back to safe defaults when none exist.
    /// </summary>
    private async Task<UserPrivacySettings> GetSettingsAsync(Guid userId)
    {
        return await _db.UserPrivacySettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.UserId == userId)
               ?? new UserPrivacySettings { UserId = userId };
    }

    /// <summary>
    /// Evaluates a <see cref="PrivacyLevel"/> against the requesting user.
    /// </summary>
    private async Task<bool> CheckLevelAsync(
        PrivacyLevel level, Guid targetUserId, Guid? requesterId)
    {
        return level switch
        {
            PrivacyLevel.Everyone => true,

            // FriendsOfFriends: any authenticated user who is a direct friend
            // (extending to true FoF would require an extra query; Friends is the
            //  practical gate here as FoF data isn't yet fully surfaced).
            PrivacyLevel.FriendsOfFriends =>
                requesterId.HasValue && await AreFriendsAsync(targetUserId, requesterId.Value),

            PrivacyLevel.Friends =>
                requesterId.HasValue && await AreFriendsAsync(targetUserId, requesterId.Value),

            PrivacyLevel.OnlyMe => false,

            _ => false,
        };
    }
}
