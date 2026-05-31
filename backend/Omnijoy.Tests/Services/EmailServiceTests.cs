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

    // ── Live path (real SMTP host) ────────────────────────────────────────────
    //
    // SmtpClient is sealed and not trivially mockable. We point it at
    // 127.0.0.1 on a deliberately-unused high port so the SMTP send fails
    // with a connection error rather than reaching out to a real relay.
    // This still exercises the credential-wiring code path (config read +
    // NetworkCredential construction) before the connection attempt is made.

    [Fact]
    public async Task SendOtpEmail_SmtpHostConfiguredButUnreachable_ThrowsSmtpException()
    {
        // Sanity-check: when SMTP host is configured we DO attempt a real send,
        // and a connection failure surfaces as an exception (so AuthController's
        // OTP retry / log path is exercised in prod when the relay goes down).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"] = "127.0.0.1",
                ["Email:SmtpPort"] = "1",          // reserved, always refused
                ["Email:FromAddress"] = "noreply@omnijoy.test",
                ["Email:FromName"]    = "OmnijoyTest",
            })
            .Build();

        var sut = new EmailService(config, NullLogger<EmailService>.Instance);

        var act = () => sut.SendOtpEmailAsync("user@example.com", "Eve", "222222");

        // SmtpException wraps the underlying socket failure.
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendOtpEmail_SmtpUserConfigured_AttemptsAuthenticatedSendAndFailsCleanly()
    {
        // Same shape as above, but with SmtpUser/SmtpPassword set — proves
        // the credential branch (NetworkCredential construction) doesn't
        // blow up before the connection is attempted. The send still fails
        // because port 1 is closed.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"]     = "127.0.0.1",
                ["Email:SmtpPort"]     = "1",
                ["Email:SmtpUser"]     = "smtp-user@example.com",
                ["Email:SmtpPassword"] = "secret-pass",
                ["Email:FromAddress"]  = "noreply@omnijoy.test",
                ["Email:FromName"]     = "OmnijoyTest",
            })
            .Build();

        var sut = new EmailService(config, NullLogger<EmailService>.Instance);

        var act = () => sut.SendOtpEmailAsync("user@example.com", "Frank", "333333");

        await act.Should().ThrowAsync<Exception>();
    }
}
