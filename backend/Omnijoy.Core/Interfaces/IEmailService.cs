namespace Omnijoy.Core.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string displayName, string otpCode);
}
