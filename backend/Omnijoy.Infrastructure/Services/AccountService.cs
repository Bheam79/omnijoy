using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly OmnijoyDbContext _db;

    public AccountService(OmnijoyDbContext db)
    {
        _db = db;
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
}
