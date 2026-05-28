using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly OmnijoyDbContext        _db;
    private readonly IModerationLogService   _moderationLog;

    public AdminService(OmnijoyDbContext db, IModerationLogService moderationLog)
    {
        _db            = db;
        _moderationLog = moderationLog;
    }

    /// <inheritdoc />
    public async Task<AdminUserDto> ChangeUserRoleAsync(
        Guid requesterId,
        Guid targetUserId,
        string newRole)
    {
        if (!Enum.TryParse<UserRole>(newRole, ignoreCase: true, out var role))
            throw new ArgumentException(
                $"Invalid role '{newRole}'. Allowed values: User, Moderator, Admin.");

        var target = await _db.Users.FindAsync(targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        var previous = target.Role;

        target.Role      = role;
        target.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _moderationLog.LogAsync(
            actorId:    requesterId,
            action:     ModerationAction.ChangeRole,
            targetType: "User",
            targetId:   target.Id.ToString(),
            notes:      $"{previous} -> {role}");

        return MapToDto(target);
    }

    /// <inheritdoc />
    public async Task<AdminUserDto> BanUserAsync(
        Guid requesterId,
        Guid targetUserId,
        string? notes)
    {
        var target = await _db.Users.FindAsync(targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        target.IsBanned  = true;
        target.BannedAt  = DateTime.UtcNow;
        target.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _moderationLog.LogAsync(
            actorId:    requesterId,
            action:     ModerationAction.BanUser,
            targetType: "User",
            targetId:   target.Id.ToString(),
            notes:      notes);

        return MapToDto(target);
    }

    /// <inheritdoc />
    public async Task<AdminUserDto> UnbanUserAsync(
        Guid requesterId,
        Guid targetUserId,
        string? notes)
    {
        var target = await _db.Users.FindAsync(targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        target.IsBanned  = false;
        target.BannedAt  = null;
        target.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _moderationLog.LogAsync(
            actorId:    requesterId,
            action:     ModerationAction.UnbanUser,
            targetType: "User",
            targetId:   target.Id.ToString(),
            notes:      notes);

        return MapToDto(target);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    internal static AdminUserDto MapToDto(Core.Models.User u) => new(
        Id:          u.Id,
        Email:       u.Email,
        DisplayName: u.DisplayName,
        Role:        u.Role.ToString(),
        IsActive:    u.IsActive,
        IsBanned:    u.IsBanned,
        BannedAt:    u.BannedAt,
        CreatedAt:   u.CreatedAt
    );
}
