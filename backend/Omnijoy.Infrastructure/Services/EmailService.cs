using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string toEmail, string displayName, string otpCode)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPortStr = _configuration["Email:SmtpPort"];
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@omnijoy.local";
        var fromName = _configuration["Email:FromName"] ?? "Omnijoy";

        var body = $@"Hi {displayName},

Your Omnijoy one-time passcode is:

    {otpCode}

This code expires in 10 minutes. Do not share it with anyone.

— The Omnijoy Team";

        // If no SMTP host configured, just log the OTP (handy for development)
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation(
                "📧 [DEV EMAIL] To: {Email} | OTP: {Code}", toEmail, otpCode);
            return;
        }

        var port = int.TryParse(smtpPortStr, out var p) ? p : 25;

        using var client = new SmtpClient(smtpHost, port);
        client.EnableSsl = port == 587 || port == 465;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;

        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPass = _configuration["Email:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(smtpUser))
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = $"Your Omnijoy verification code: {otpCode}",
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }

    public async Task SendFriendInviteEmailAsync(
        string toEmail,
        string inviterDisplayName,
        string inviteUrl)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPortStr = _configuration["Email:SmtpPort"];
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@omnijoy.local";
        var fromName = _configuration["Email:FromName"] ?? "Omnijoy";

        var body = $@"Hi there,

{inviterDisplayName} has invited you to join Omnijoy!

Click the link below to create your account and connect with {inviterDisplayName} automatically:

    {inviteUrl}

This invite link expires in 30 days.

— The Omnijoy Team";

        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation(
                "📧 [DEV EMAIL] Friend invite | To: {Email} | InviteUrl: {Url}",
                toEmail, inviteUrl);
            return;
        }

        var port = int.TryParse(smtpPortStr, out var p) ? p : 25;

        using var client = new SmtpClient(smtpHost, port);
        client.EnableSsl = port == 587 || port == 465;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;

        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPass = _configuration["Email:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(smtpUser))
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = $"{inviterDisplayName} invited you to Omnijoy",
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationUrl)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPortStr = _configuration["Email:SmtpPort"];
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@omnijoy.local";
        var fromName = _configuration["Email:FromName"] ?? "Omnijoy";

        // HTML body modelled on the password-reset template — same brand
        // chrome, single call-to-action button, plus a plain-text fallback
        // link in case the button is stripped.
        var safeName = System.Net.WebUtility.HtmlEncode(displayName);
        var safeUrl = System.Net.WebUtility.HtmlEncode(verificationUrl);
        var htmlBody = $@"<!DOCTYPE html>
<html>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #1f2937; max-width: 560px; margin: 0 auto; padding: 24px;"">
  <h2 style=""color: #4f46e5; margin-bottom: 16px;"">Welcome to Omnijoy, {safeName}!</h2>
  <p>Please confirm your email address so we know it's really you. Click the button below to finish setting up your account:</p>
  <p style=""text-align: center; margin: 32px 0;"">
    <a href=""{safeUrl}"" style=""background: #4f46e5; color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 6px; font-weight: 600; display: inline-block;"">Verify my email</a>
  </p>
  <p style=""color: #6b7280; font-size: 14px;"">If the button doesn't work, copy and paste this link into your browser:</p>
  <p style=""word-break: break-all; color: #4f46e5; font-size: 14px;""><a href=""{safeUrl}"" style=""color: #4f46e5;"">{safeUrl}</a></p>
  <p style=""color: #6b7280; font-size: 13px; margin-top: 32px;"">If you didn't create an Omnijoy account, you can safely ignore this email.</p>
  <p style=""color: #9ca3af; font-size: 12px;"">— The Omnijoy Team</p>
</body>
</html>";

        // If no SMTP host is configured, log the verification URL (handy for
        // local development and the E2E suite, which scrapes log output).
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation(
                "📧 [DEV EMAIL] To: {Email} | Verify URL: {Url}", toEmail, verificationUrl);
            return;
        }

        var port = int.TryParse(smtpPortStr, out var p) ? p : 25;

        using var client = new SmtpClient(smtpHost, port);
        client.EnableSsl = port == 587 || port == 465;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;

        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPass = _configuration["Email:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(smtpUser))
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = "Verify your Omnijoy email address",
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetCode)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPortStr = _configuration["Email:SmtpPort"];
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@omnijoy.local";
        var fromName = _configuration["Email:FromName"] ?? "Omnijoy";

        var body = $@"Hi {displayName},

You requested a password reset for your Omnijoy account.

Your password reset code is:

    {resetCode}

This code expires in 15 minutes. Do not share it with anyone.

If you did not request a password reset, you can safely ignore this email.

— The Omnijoy Team";

        // If no SMTP host configured, just log the code (handy for development)
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation(
                "📧 [DEV EMAIL] To: {Email} | Password reset code: {Code}", toEmail, resetCode);
            return;
        }

        var port = int.TryParse(smtpPortStr, out var p) ? p : 25;

        using var client = new SmtpClient(smtpHost, port);
        client.EnableSsl = port == 587 || port == 465;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;

        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPass = _configuration["Email:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(smtpUser))
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = "Reset your Omnijoy password",
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }
}
