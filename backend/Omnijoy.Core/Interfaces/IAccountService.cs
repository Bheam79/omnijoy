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

    /// <summary>
    /// Marks the user account as deactivated. The user is hidden from search
    /// and feed, all refresh tokens are revoked, but the account can be
    /// re-activated by logging in with the correct credentials.
    /// </summary>
    Task DeactivateAccountAsync(Guid userId);

    /// <summary>
    /// Schedules the user account for permanent deletion. By default, the
    /// account is soft-deleted with a 30-day grace period during which it
    /// can still be restored by support. When the `Account:HardDelete`
    /// config flag is true, the account is removed immediately.
    /// </summary>
    /// <param name="userId">The authenticated user.</param>
    /// <param name="confirmEmail">
    /// The user must re-enter their own email address to confirm; this
    /// guards against accidental clicks.
    /// </param>
    Task DeleteAccountAsync(Guid userId, string confirmEmail);
}
