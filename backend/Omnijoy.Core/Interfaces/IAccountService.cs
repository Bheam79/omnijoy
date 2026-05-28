using Omnijoy.Core.DTOs.Notifications;

namespace Omnijoy.Core.Interfaces;

public interface IAccountService
{
    /// <summary>
    /// Changes the authenticated user's email address after verifying their current password.
    /// Updates the email directly (no email service required).
    /// </summary>
    Task ChangeEmailAsync(Guid userId, string newEmail, string currentPassword);

    /// <summary>
    /// Changes the authenticated user's password after verifying their current password.
    /// </summary>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string confirmNewPassword);

    /// <summary>
    /// Returns the user's notification preferences. If no row exists yet
    /// (older accounts) a default ("everything on") row is created on the fly.
    /// </summary>
    Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(Guid userId);

    /// <summary>
    /// Persists notification preferences for the user and returns the updated DTO.
    /// </summary>
    Task<NotificationPreferencesDto> UpdateNotificationPreferencesAsync(
        Guid userId,
        NotificationPreferencesDto prefs);
}
