using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly OmnijoyDbContext _db;
    private readonly IConfiguration? _config;

    public AccountService(OmnijoyDbContext db, IConfiguration? config = null)
    {
        _db = db;
        _config = config;
    }

    // ── Change email ──────────────────────────────────────────────────────────

    public async Task ChangeEmailAsync(Guid userId, string newEmail, string currentPassword)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("Email is required.");

        // Basic email format check
        newEmail = newEmail.Trim().ToLowerInvariant();
        if (!newEmail.Contains('@') || !newEmail.Contains('.'))
            throw new ArgumentException("Invalid email format.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Verify current password
        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        // Check email uniqueness
        if (await _db.Users.AnyAsync(u => u.Email == newEmail && u.Id != userId))
            throw new InvalidOperationException("An account with this email already exists.");

        user.Email = newEmail;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Change password ───────────────────────────────────────────────────────

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string confirmNewPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("New password is required.");

        if (newPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        if (newPassword != confirmNewPassword)
            throw new ArgumentException("New password and confirmation do not match.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Verify current password
        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Notification preferences ──────────────────────────────────────────────

    public async Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(Guid userId)
    {
        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (prefs is null)
        {
            // Lazily create a default row for accounts that pre-date this feature.
            if (!await _db.Users.AnyAsync(u => u.Id == userId))
                throw new KeyNotFoundException("User not found.");

            prefs = new NotificationPreferences { UserId = userId };
            _db.NotificationPreferences.Add(prefs);
            await _db.SaveChangesAsync();
        }

        return ToDto(prefs);
    }

    public async Task<NotificationPreferencesDto> UpdateNotificationPreferencesAsync(
        Guid userId,
        NotificationPreferencesDto dto)
    {
        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (prefs is null)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == userId))
                throw new KeyNotFoundException("User not found.");

            prefs = new NotificationPreferences { UserId = userId };
            _db.NotificationPreferences.Add(prefs);
        }

        prefs.LikesOnMyPosts      = dto.LikesOnMyPosts;
        prefs.CommentsOnMyPosts   = dto.CommentsOnMyPosts;
        prefs.PostShares          = dto.PostShares;
        prefs.FriendRequests      = dto.FriendRequests;
        prefs.NewFollower         = dto.NewFollower;
        prefs.Mentions            = dto.Mentions;
        prefs.NewPostsFromFriends = dto.NewPostsFromFriends;
        prefs.FamilyRelations     = dto.FamilyRelations;
        prefs.EventInvites        = dto.EventInvites;
        prefs.DirectMessages      = dto.DirectMessages;
        prefs.LiveStreams         = dto.LiveStreams;
        prefs.CompanyPageInvites  = dto.CompanyPageInvites;

        await _db.SaveChangesAsync();
        return ToDto(prefs);
    }

    // ── Deactivate / delete account ───────────────────────────────────────────

    public async Task DeactivateAccountAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.DeletionScheduledAt is not null)
            throw new InvalidOperationException("Account is pending deletion and cannot be deactivated.");

        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.DeactivatedAt = now;
        user.UpdatedAt = now;

        // Revoke all outstanding refresh tokens so existing sessions can't
        // silently refresh themselves back into the app.
        await RevokeRefreshTokensAsync(userId);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAccountAsync(Guid userId, string confirmEmail)
    {
        if (string.IsNullOrWhiteSpace(confirmEmail))
            throw new ArgumentException("Email confirmation is required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        var normalizedConfirm = confirmEmail.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, normalizedConfirm, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Email confirmation does not match the account email.");

        // Revoke all outstanding refresh tokens regardless of hard/soft path.
        await RevokeRefreshTokensAsync(userId);

        var hardDelete = _config?.GetValue<bool>("Account:HardDelete") ?? false;
        if (hardDelete)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return;
        }

        // Soft-delete: mark inactive + schedule purge after the grace period.
        // The grace period default is 30 days; a background job (out of scope)
        // would later remove rows whose DeletionScheduledAt + grace <= now.
        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.DeactivatedAt ??= now;
        user.DeletionScheduledAt = now;
        user.UpdatedAt = now;

        await _db.SaveChangesAsync();
    }

    private async Task RevokeRefreshTokensAsync(Guid userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
            t.IsRevoked = true;
    }

    private static NotificationPreferencesDto ToDto(NotificationPreferences p) => new(
        LikesOnMyPosts:      p.LikesOnMyPosts,
        CommentsOnMyPosts:   p.CommentsOnMyPosts,
        PostShares:          p.PostShares,
        FriendRequests:      p.FriendRequests,
        NewFollower:         p.NewFollower,
        Mentions:            p.Mentions,
        NewPostsFromFriends: p.NewPostsFromFriends,
        FamilyRelations:     p.FamilyRelations,
        EventInvites:        p.EventInvites,
        DirectMessages:      p.DirectMessages,
        LiveStreams:         p.LiveStreams,
        CompanyPageInvites:  p.CompanyPageInvites
    );
}
