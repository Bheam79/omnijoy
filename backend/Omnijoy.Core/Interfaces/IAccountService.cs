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
}
