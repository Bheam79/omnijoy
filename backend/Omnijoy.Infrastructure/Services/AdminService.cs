using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly OmnijoyDbContext _db;

    public AdminService(OmnijoyDbContext db)
    {
        _db = db;
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

        target.Role      = role;
        target.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return MapToDto(target);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    internal static AdminUserDto MapToDto(Core.Models.User u) => new(
        Id:          u.Id,
        Email:       u.Email,
        DisplayName: u.DisplayName,
        Role:        u.Role.ToString(),
        IsActive:    u.IsActive,
        CreatedAt:   u.CreatedAt
    );
}
