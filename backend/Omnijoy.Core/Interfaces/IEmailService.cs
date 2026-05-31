namespace Omnijoy.Core.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string displayName, string otpCode);

    Task SendFriendInviteEmailAsync(
        string toEmail,
        string inviterDisplayName,
        string inviteUrl);
}
