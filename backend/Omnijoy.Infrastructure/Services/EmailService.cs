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
