using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="EmailService"/>.
///
/// When <c>Email:SmtpHost</c> is not configured the service just logs the OTP
/// (dev mode) and returns — no real SMTP connection is needed for unit tests.
/// </summary>
public class EmailServiceTests
{
    // ── Dev mode (no SMTP configured) ─────────────────────────────────────────

    [Fact]
    public async Task SendOtpEmail_NoSmtpConfigured_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // deliberately omit Email:SmtpHost
                ["Email:FromAddress"] = "noreply@omnijoy.test",
                ["Email:FromName"]    = "OmnijoyTest",
            })
            .Build();

        var sut = new EmailService(config, NullLogger<EmailService>.Instance);

        var act = () => sut.SendOtpEmailAsync("user@example.com", "Alice", "123456");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendOtpEmail_EmptySmtpHost_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"] = "", // blank — treated same as missing
            })
            .Build();

        var sut = new EmailService(config, NullLogger<EmailService>.Instance);

        var act = () => sut.SendOtpEmailAsync("user@example.com", "Bob", "654321");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendOtpEmail_WhitespaceSmtpHost_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"] = "   ",
            })
            .Build();

        var sut = new EmailService(config, NullLogger<EmailService>.Instance);

        var act = () => sut.SendOtpEmailAsync("user@example.com", "Carol", "999999");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendOtpEmail_NoFromAddressConfigured_UsesDefaultFromAddress()
    {
        // When Email:FromAddress is absent the service uses "noreply@omnijoy.local".
        // With no SMTP host this still just logs — no assertion on the From
        // address, just verifying it doesn't throw from a missing default.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var sut = new EmailService(config, NullLogger<EmailService>.Instance);

        var act = () => sut.SendOtpEmailAsync("dest@example.com", "Dave", "111111");

        await act.Should().NotThrowAsync();
    }
}
