namespace Omnijoy.Core.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string displayName, string otpCode);

    Task SendFriendInviteEmailAsync(
        string toEmail,
        string inviterDisplayName,
        string inviteUrl);

    Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetCode);

    /// <summary>
    /// Sends the "please confirm your email address" message containing the
    /// one-click verification link the user follows to flip
    /// <c>User.IsEmailVerified</c> to true.
    /// </summary>
    /// <param name="toEmail">Recipient address.</param>
    /// <param name="displayName">Greeting name shown in the body.</param>
    /// <param name="verificationUrl">Fully-formed link including the token.</param>
    Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationUrl);
}
