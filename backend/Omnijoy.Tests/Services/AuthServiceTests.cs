using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<ITokenService> _tokensMock;
    private readonly Mock<IEmailService> _emailMock;
    private readonly Mock<IHttpClientFactory> _httpFactoryMock;
    private readonly Mock<ITokenBlacklist> _blacklistMock;
    private readonly Mock<INotificationService> _notificationsMock;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);

        _tokensMock = new Mock<ITokenService>();
        _tokensMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("fake-access-token");
        _tokensMock.Setup(t => t.GenerateRefreshToken()).Returns(("fake-raw-token", "fake-hash"));
        _tokensMock.SetupGet(t => t.AccessTokenExpirySeconds).Returns(3600);

        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _emailMock.Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _httpFactoryMock = new Mock<IHttpClientFactory>();

        _blacklistMock = new Mock<ITokenBlacklist>();
        _blacklistMock
            .Setup(b => b.BlacklistAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        _blacklistMock
            .Setup(b => b.IsBlacklistedAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _notificationsMock = new Mock<INotificationService>();
        _notificationsMock
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new Omnijoy.Core.DTOs.Notifications.NotificationDto(
                Guid.NewGuid(), "PasswordReset", null, false, DateTime.UtcNow, null, null, null));

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiryDays"] = "30",
            })
            .Build();

        _sut = new AuthService(_db, _tokensMock.Object, _emailMock.Object, _config, _httpFactoryMock.Object, _blacklistMock.Object, _notificationsMock.Object, NullLogger<AuthService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> RegisterPasswordUserAsync(string email = "test@example.com", string password = "P@ssw0rd!")
    {
        await _sut.RegisterAsync(new RegisterRequest(
            Email: email,
            DisplayName: "Test User",
            AuthMethod: "password",
            Password: password,
            LocationCountry: "Norway"));
        return await _db.Users.FirstAsync(u => u.Email == email.ToLowerInvariant());
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_PasswordAuth_CreatesUserAndReturnsResponse()
    {
        var request = new RegisterRequest(
            Email: "alice@example.com",
            DisplayName: "Alice",
            AuthMethod: "password",
            Password: "P@ssw0rd!",
            LocationCountry: "France",
            LocationCity: "Paris",
            LocationName: "Paris, France");

        var response = await _sut.RegisterAsync(request);

        response.Should().NotBeNull();
        response.AccessToken.Should().Be("fake-access-token");
        response.User.Email.Should().Be("alice@example.com");
        response.User.DisplayName.Should().Be("Alice");
        response.User.LocationCountry.Should().Be("France");
        response.User.LocationCity.Should().Be("Paris");

        var user = await _db.Users.FirstAsync();
        user.Email.Should().Be("alice@example.com");
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.LocationCountry.Should().Be("France");
        user.LocationCity.Should().Be("Paris");

        var authProvider = await _db.AuthProviders.FirstAsync();
        authProvider.Provider.Should().Be(AuthProviderType.Password);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsInvalidOp()
    {
        await RegisterPasswordUserAsync("dup@example.com");

        await _sut.Invoking(s => s.RegisterAsync(new RegisterRequest(
                Email: "dup@example.com",
                DisplayName: "Second",
                AuthMethod: "password",
                Password: "P@ss!",
                LocationCountry: "Norway")))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Register_InvalidAuthMethod_ThrowsArgumentException()
    {
        var request = new RegisterRequest("x@example.com", "X", "magic-link", null);

        await _sut.Invoking(s => s.RegisterAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*authMethod*");
    }

    [Fact]
    public async Task Register_PasswordAuthWithNoPassword_ThrowsArgumentException()
    {
        var request = new RegisterRequest("x@example.com", "X", "password", null);

        await _sut.Invoking(s => s.RegisterAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Password*");
    }

    [Fact]
    public async Task Register_MissingCountry_ThrowsArgumentException()
    {
        // Registration must require a country selection so the frontend can
        // show the location picker before completing sign-up.
        var request = new RegisterRequest(
            Email: "noplace@example.com",
            DisplayName: "No Place",
            AuthMethod: "password",
            Password: "P@ssw0rd!");

        await _sut.Invoking(s => s.RegisterAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Please select your location to continue*");
    }

    [Fact]
    public async Task Register_OtpAuth_DoesNotRequirePassword()
    {
        var request = new RegisterRequest(
            Email: "otp@example.com",
            DisplayName: "OTP User",
            AuthMethod: "otp",
            Password: null,
            LocationCountry: "Germany");

        var response = await _sut.RegisterAsync(request);

        response.User.Email.Should().Be("otp@example.com");
        var provider = await _db.AuthProviders.FirstAsync();
        provider.Provider.Should().Be(AuthProviderType.OTP);
    }

    // ── LoginWithPassword ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoginWithPassword_ValidCredentials_ReturnsAuthResponse()
    {
        await RegisterPasswordUserAsync("login@example.com", "P@ssw0rd!");

        var response = await _sut.LoginWithPasswordAsync(new LoginPasswordRequest("login@example.com", "P@ssw0rd!"));

        response.Should().NotBeNull();
        response.AccessToken.Should().Be("fake-access-token");
        response.User.Email.Should().Be("login@example.com");
    }

    [Fact]
    public async Task LoginWithPassword_WrongPassword_ThrowsUnauthorized()
    {
        await RegisterPasswordUserAsync("login@example.com", "correct-password");

        await _sut.Invoking(s => s.LoginWithPasswordAsync(
                new LoginPasswordRequest("login@example.com", "wrong-password")))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginWithPassword_UserNotFound_ThrowsUnauthorized()
    {
        await _sut.Invoking(s => s.LoginWithPasswordAsync(
                new LoginPasswordRequest("nobody@example.com", "any-password")))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginWithPassword_DeactivatedAccount_ReactivatesAndSignsIn()
    {
        var user = await RegisterPasswordUserAsync("deact@example.com", "P@ssw0rd!");
        user.IsActive = false;
        user.DeactivatedAt = DateTime.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        var response = await _sut.LoginWithPasswordAsync(
            new LoginPasswordRequest("deact@example.com", "P@ssw0rd!"));

        response.Should().NotBeNull();
        var reloaded = await _db.Users.FirstAsync(u => u.Id == user.Id);
        reloaded.IsActive.Should().BeTrue();
        reloaded.DeactivatedAt.Should().BeNull();
    }

    [Fact]
    public async Task LoginWithPassword_PendingDeletion_ThrowsUnauthorized()
    {
        var user = await RegisterPasswordUserAsync("doomed@example.com", "P@ssw0rd!");
        user.IsActive = false;
        user.DeactivatedAt = DateTime.UtcNow;
        user.DeletionScheduledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.LoginWithPasswordAsync(
                new LoginPasswordRequest("doomed@example.com", "P@ssw0rd!")))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*deleted*");
    }

    // ── OTP ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestOtp_ExistingUser_SavesCodeAndSendsEmail()
    {
        await RegisterPasswordUserAsync("otp@example.com");

        await _sut.RequestOtpAsync(new OtpRequestDto("otp@example.com"));

        var otpRecord = await _db.OtpCodes.FirstAsync();
        otpRecord.Email.Should().Be("otp@example.com");
        otpRecord.IsUsed.Should().BeFalse();
        otpRecord.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        _emailMock.Verify(e => e.SendOtpEmailAsync("otp@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RequestOtp_NonExistingUser_DoesNothing()
    {
        // Should silently succeed (prevents email enumeration)
        await _sut.Invoking(s => s.RequestOtpAsync(new OtpRequestDto("ghost@example.com")))
            .Should().NotThrowAsync();

        (await _db.OtpCodes.CountAsync()).Should().Be(0);
        _emailMock.Verify(e => e.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyOtp_InvalidCode_ThrowsUnauthorized()
    {
        await RegisterPasswordUserAsync("otp@example.com");
        await _sut.RequestOtpAsync(new OtpRequestDto("otp@example.com"));

        await _sut.Invoking(s => s.VerifyOtpAsync(new OtpVerifyDto("otp@example.com", "000000")))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid or expired OTP*");
    }

    [Fact]
    public async Task VerifyOtp_ExpiredCode_ThrowsUnauthorized()
    {
        await RegisterPasswordUserAsync("otp@example.com");

        // Insert an already-expired OTP manually
        _db.OtpCodes.Add(new OtpCode
        {
            Id        = Guid.NewGuid(),
            Email     = "otp@example.com",
            CodeHash  = "some-hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // expired
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
            IsUsed    = false,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.VerifyOtpAsync(new OtpVerifyDto("otp@example.com", "123456")))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_IssuesNewTokensAndRevokesOld()
    {
        var user = await RegisterPasswordUserAsync("refresh@example.com");

        // Store a refresh token directly
        var rawToken = "valid-refresh-token";
        var hash = TokenService.HashToken(rawToken);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            User      = user,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false,
        });
        await _db.SaveChangesAsync();

        var response = await _sut.RefreshAsync(rawToken);

        response.Should().NotBeNull();
        response.AccessToken.Should().Be("fake-access-token");

        // Old token should be revoked
        var oldToken = await _db.RefreshTokens.FirstAsync(rt => rt.TokenHash == hash);
        oldToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_InvalidToken_ThrowsUnauthorized()
    {
        await _sut.Invoking(s => s.RefreshAsync("bogus-token-that-does-not-exist"))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid or expired refresh token*");
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidToken_RevokesRefreshToken()
    {
        var user = await RegisterPasswordUserAsync("logout@example.com");

        var rawToken = "logout-refresh-token";
        var hash = TokenService.HashToken(rawToken);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            User      = user,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false,
        });
        await _db.SaveChangesAsync();

        await _sut.LogoutAsync(rawToken);

        var stored = await _db.RefreshTokens.FirstAsync(rt => rt.TokenHash == hash);
        stored.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_UnknownToken_DoesNotThrow()
    {
        await _sut.Invoking(s => s.LogoutAsync("unknown-token"))
            .Should().NotThrowAsync();
    }

    // ── Helpers for password-reset tests ──────────────────────────────────────

    /// <summary>
    /// Replicates the private HashCode helper in AuthService so tests can
    /// pre-seed OtpCode rows with a known, submittable plain-text code.
    /// </summary>
    private static string ComputeCodeHash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Seeds a PasswordReset (or other-purpose) OtpCode row with a fixed
    /// known code ("654321") so tests can submit it without cracking the hash.
    /// </summary>
    private async Task<(OtpCode otp, string plainCode)> SeedPasswordResetOtpAsync(
        string email,
        int attemptCount = 0,
        bool isUsed = false,
        DateTime? expiresAt = null,
        OtpPurpose purpose = OtpPurpose.PasswordReset)
    {
        const string code = "654321";
        var otp = new OtpCode
        {
            Id           = Guid.NewGuid(),
            Email        = email,
            CodeHash     = ComputeCodeHash(code),
            ExpiresAt    = expiresAt ?? DateTime.UtcNow.AddMinutes(15),
            CreatedAt    = DateTime.UtcNow,
            IsUsed       = isUsed,
            Purpose      = purpose,
            AttemptCount = attemptCount,
        };
        _db.OtpCodes.Add(otp);
        await _db.SaveChangesAsync();
        return (otp, code);
    }

    // ── RequestPasswordResetAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RequestPasswordReset_KnownEmail_PersistsPasswordResetOtpAndSendsEmail()
    {
        var user = await RegisterPasswordUserAsync("reset@example.com");

        await _sut.RequestPasswordResetAsync(new PasswordResetRequestDto { Email = "reset@example.com" });

        var otpRecord = await _db.OtpCodes.FirstAsync(o => o.Purpose == OtpPurpose.PasswordReset);
        otpRecord.Email.Should().Be("reset@example.com");
        otpRecord.IsUsed.Should().BeFalse();
        otpRecord.AttemptCount.Should().Be(0);
        otpRecord.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));

        _emailMock.Verify(e => e.SendPasswordResetEmailAsync(
            "reset@example.com",
            user.DisplayName,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RequestPasswordReset_UnknownEmail_ReturnsSilentlyWithoutInsertingRow()
    {
        await _sut.Invoking(s => s.RequestPasswordResetAsync(
                new PasswordResetRequestDto { Email = "ghost@example.com" }))
            .Should().NotThrowAsync();

        (await _db.OtpCodes.CountAsync()).Should().Be(0);
        _emailMock.Verify(e => e.SendPasswordResetEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestPasswordReset_DoesNotAffectExistingLoginOtps()
    {
        await RegisterPasswordUserAsync("reset2@example.com");

        // Pre-seed a Login-purpose OTP
        _db.OtpCodes.Add(new OtpCode
        {
            Id           = Guid.NewGuid(),
            Email        = "reset2@example.com",
            CodeHash     = "some-login-hash",
            ExpiresAt    = DateTime.UtcNow.AddMinutes(10),
            CreatedAt    = DateTime.UtcNow,
            IsUsed       = false,
            Purpose      = OtpPurpose.Login,
            AttemptCount = 0,
        });
        await _db.SaveChangesAsync();

        await _sut.RequestPasswordResetAsync(new PasswordResetRequestDto { Email = "reset2@example.com" });

        // The Login OTP must be completely untouched
        var loginOtp = await _db.OtpCodes.FirstAsync(o => o.Purpose == OtpPurpose.Login);
        loginOtp.IsUsed.Should().BeFalse();
        loginOtp.CodeHash.Should().Be("some-login-hash");
        loginOtp.AttemptCount.Should().Be(0);
    }

    // ── ConfirmPasswordResetAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPasswordReset_HappyPath_RotatesHashAndRevokesAllRefreshTokens()
    {
        var user = await RegisterPasswordUserAsync("confirm@example.com", "OldPassword123!");
        var oldHash = user.PasswordHash;

        // Seed two additional active refresh tokens (Register already created one)
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            User      = user,
            TokenHash = "manual-token-hash-1",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false,
        });
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            User      = user,
            TokenHash = "manual-token-hash-2",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false,
        });
        await _db.SaveChangesAsync();

        var (otp, code) = await SeedPasswordResetOtpAsync("confirm@example.com");

        await _sut.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
        {
            Email       = "confirm@example.com",
            Code        = code,
            NewPassword = "NewPassword123!",
        });

        // Password hash rotated
        var updatedUser = await _db.Users.FirstAsync(u => u.Email == "confirm@example.com");
        BCrypt.Net.BCrypt.Verify("NewPassword123!", updatedUser.PasswordHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("OldPassword123!", updatedUser.PasswordHash).Should().BeFalse();

        // All refresh tokens for this user are now revoked
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();
        tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());

        // TokenInvalidationCutoffUtc must be set so outstanding JWTs are rejected
        updatedUser.TokenInvalidationCutoffUtc.Should().NotBeNull();
        updatedUser.TokenInvalidationCutoffUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // OTP marked used
        var otpRow = await _db.OtpCodes.FirstAsync(o => o.Id == otp.Id);
        otpRow.IsUsed.Should().BeTrue();

        // Security notification pushed (persisted)
        _notificationsMock.Verify(n => n.CreateAsync(
            user.Id, NotificationType.PasswordReset, null, null), Times.Once);
    }

    [Fact]
    public async Task ConfirmPasswordReset_SetsTokenInvalidationCutoff_ToApproximatelyNow()
    {
        // Arrange
        var user = await RegisterPasswordUserAsync("cutoff@example.com", "OldPassword!");
        user.TokenInvalidationCutoffUtc.Should().BeNull("cutoff is null before any reset");

        var before = DateTime.UtcNow;
        var (_, code) = await SeedPasswordResetOtpAsync("cutoff@example.com");

        // Act
        await _sut.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
        {
            Email       = "cutoff@example.com",
            Code        = code,
            NewPassword = "NewPassword123!",
        });

        // Assert
        var after = DateTime.UtcNow;
        var reloaded = await _db.Users.FirstAsync(u => u.Email == "cutoff@example.com");
        reloaded.TokenInvalidationCutoffUtc.Should().NotBeNull();
        reloaded.TokenInvalidationCutoffUtc!.Value.Should().BeOnOrAfter(before);
        reloaded.TokenInvalidationCutoffUtc!.Value.Should().BeOnOrBefore(after.AddSeconds(1));
    }

    [Fact]
    public async Task ConfirmPasswordReset_WrongCode_IncrementsAttemptCountAndThrowsUnauthorized()
    {
        await RegisterPasswordUserAsync("attempt@example.com");
        var (otp, _) = await SeedPasswordResetOtpAsync("attempt@example.com");

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "attempt@example.com",
                Code        = "000000", // deliberately wrong
                NewPassword = "NewPassword123!",
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        var otpRow = await _db.OtpCodes.FirstAsync(o => o.Id == otp.Id);
        otpRow.AttemptCount.Should().Be(1);
        otpRow.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPasswordReset_ExpiredCode_Throws()
    {
        var user = await RegisterPasswordUserAsync("expired@example.com");
        var originalHash = user.PasswordHash;

        var (_, code) = await SeedPasswordResetOtpAsync(
            "expired@example.com",
            expiresAt: DateTime.UtcNow.AddMinutes(-5)); // already expired

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "expired@example.com",
                Code        = code,
                NewPassword = "NewPassword123!",
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        // Password hash must not have changed
        var reloaded = await _db.Users.FirstAsync(u => u.Email == "expired@example.com");
        reloaded.PasswordHash.Should().Be(originalHash);
    }

    [Fact]
    public async Task ConfirmPasswordReset_UnknownEmail_ThrowsSameMessageAsWrongCode()
    {
        // Seed a user + valid OTP so the wrong-code path produces its error message
        await RegisterPasswordUserAsync("known@example.com");
        await SeedPasswordResetOtpAsync("known@example.com");

        // Wrong code for a known user
        string? wrongCodeMsg = null;
        try
        {
            await _sut.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email = "known@example.com", Code = "000000", NewPassword = "NewPassword123!",
            });
        }
        catch (UnauthorizedAccessException ex) { wrongCodeMsg = ex.Message; }

        // Valid-format code for a completely unknown email (no OTP row at all)
        string? unknownEmailMsg = null;
        try
        {
            await _sut.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email = "nobody@example.com", Code = "111111", NewPassword = "NewPassword123!",
            });
        }
        catch (UnauthorizedAccessException ex) { unknownEmailMsg = ex.Message; }

        wrongCodeMsg.Should().NotBeNull();
        unknownEmailMsg.Should().NotBeNull();
        // The error message must be byte-identical — no enumeration via error text
        wrongCodeMsg.Should().Be(unknownEmailMsg);
    }

    [Fact]
    public async Task ConfirmPasswordReset_TooManyAttempts_LocksOutAndMarksUsed()
    {
        await RegisterPasswordUserAsync("lockout@example.com");
        var (otp, code) = await SeedPasswordResetOtpAsync("lockout@example.com", attemptCount: 5);

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "lockout@example.com",
                Code        = code,
                NewPassword = "NewPassword123!",
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Too many attempts*");

        // Row must be locked so a fresh request is required
        var otpRow = await _db.OtpCodes.FirstAsync(o => o.Id == otp.Id);
        otpRow.IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmPasswordReset_PasswordTooShort_ThrowsArgumentException()
    {
        var user = await RegisterPasswordUserAsync("short@example.com");
        var originalHash = user.PasswordHash;
        var (otp, code) = await SeedPasswordResetOtpAsync("short@example.com");

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "short@example.com",
                Code        = code,
                NewPassword = "abc", // < 8 chars
            }))
            .Should().ThrowAsync<ArgumentException>();

        // OTP NOT marked used — user can retry with a compliant password
        var otpRow = await _db.OtpCodes.FirstAsync(o => o.Id == otp.Id);
        otpRow.IsUsed.Should().BeFalse();

        // Hash must not have rotated
        var reloaded = await _db.Users.FirstAsync(u => u.Email == "short@example.com");
        reloaded.PasswordHash.Should().Be(originalHash);
    }

    [Fact]
    public async Task ConfirmPasswordReset_EmptyPassword_ThrowsArgumentException()
    {
        await RegisterPasswordUserAsync("empty@example.com");
        var (_, code) = await SeedPasswordResetOtpAsync("empty@example.com");

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "empty@example.com",
                Code        = code,
                NewPassword = "",
            }))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConfirmPasswordReset_DoesNotAcceptLoginPurposeOtp()
    {
        // Seed an OTP that is valid in all respects EXCEPT its Purpose is Login
        await RegisterPasswordUserAsync("loginpurpose@example.com");
        var (_, code) = await SeedPasswordResetOtpAsync(
            "loginpurpose@example.com",
            purpose: OtpPurpose.Login);

        // ConfirmPasswordResetAsync must reject it
        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "loginpurpose@example.com",
                Code        = code,
                NewPassword = "NewPassword123!",
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ConfirmPasswordReset_AlreadyUsedCode_Throws()
    {
        await RegisterPasswordUserAsync("usedcode@example.com");
        var (_, code) = await SeedPasswordResetOtpAsync("usedcode@example.com", isUsed: true);

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "usedcode@example.com",
                Code        = code,
                NewPassword = "NewPassword123!",
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ConfirmPasswordReset_DeletedAccount_Throws()
    {
        var user = await RegisterPasswordUserAsync("deleted@example.com");
        user.DeletionScheduledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var originalHash = user.PasswordHash;

        var (_, code) = await SeedPasswordResetOtpAsync("deleted@example.com");

        await _sut.Invoking(s => s.ConfirmPasswordResetAsync(new PasswordResetConfirmDto
            {
                Email       = "deleted@example.com",
                Code        = code,
                NewPassword = "NewPassword123!",
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        // Hash must not have rotated
        var reloaded = await _db.Users.FirstAsync(u => u.Email == "deleted@example.com");
        reloaded.PasswordHash.Should().Be(originalHash);
    }
}
