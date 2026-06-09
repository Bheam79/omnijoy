using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly OmnijoyDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenBlacklist _blacklist;
    private readonly INotificationService _notifications;

    private const int OtpExpiryMinutes = 10;
    private const int PasswordResetExpiryMinutes = 15;

    public AuthService(
        OmnijoyDbContext db,
        ITokenService tokens,
        IEmailService email,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ITokenBlacklist blacklist,
        INotificationService notifications)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _blacklist = blacklist;
        _notifications = notifications;
    }

    // ── Register ─────────────────────────────────────────────────────────────

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant()))
            throw new InvalidOperationException("An account with this email already exists.");

        var authMethod = request.AuthMethod.ToLowerInvariant();
        if (authMethod is not ("password" or "otp"))
            throw new ArgumentException("authMethod must be 'password' or 'otp'.");

        if (authMethod == "password" && string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required for password authentication.");

        if (string.IsNullOrWhiteSpace(request.LocationCountry))
            throw new ArgumentException("Please select your location to continue.");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant().Trim(),
            DisplayName = request.DisplayName.Trim(),
            Gender = Enum.TryParse<Gender>(request.Gender, true, out var g) ? g : Gender.NotDisclosed,
            BirthDate = request.BirthDate,
            ShowBirthDate = request.ShowBirthDate,
            LocationPlaceId = request.LocationPlaceId,
            LocationName = request.LocationName,
            LocationCity = request.LocationCity,
            LocationCountry = request.LocationCountry,
            LocationCountryCode = request.LocationCountryCode,
            LocationLatitude = request.LocationLatitude,
            LocationLongitude = request.LocationLongitude,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (authMethod == "password")
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Default privacy settings
        user.PrivacySettings = new UserPrivacySettings { UserId = user.Id };

        // Default notification preferences (all enabled)
        user.NotificationPreferences = new NotificationPreferences { UserId = user.Id };

        // Auth provider record
        _db.Users.Add(user);
        _db.AuthProviders.Add(new AuthProvider
        {
            UserId = user.Id,
            Provider = authMethod == "password" ? AuthProviderType.Password : AuthProviderType.OTP,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    // ── Login with password ───────────────────────────────────────────────────

    public async Task<AuthResponse> LoginWithPasswordAsync(LoginPasswordRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        // Accounts pending permanent deletion cannot be reactivated by login.
        if (user.DeletionScheduledAt is not null)
            throw new UnauthorizedAccessException("This account has been deleted.");

        await ReactivateIfNeededAsync(user);

        return await IssueTokensAsync(user);
    }

    private async Task ReactivateIfNeededAsync(User user)
    {
        if (user.IsActive && user.DeactivatedAt is null)
            return;

        user.IsActive = true;
        user.DeactivatedAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── OTP: request code ─────────────────────────────────────────────────────

    public async Task RequestOtpAsync(OtpRequestDto request)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always respond successfully to prevent email enumeration
        if (user is null)
            return;

        // Generate 6-digit code
        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);
        var now = DateTime.UtcNow;

        _db.OtpCodes.Add(new OtpCode
        {
            Id = Guid.NewGuid(),
            Email = email,
            CodeHash = codeHash,
            ExpiresAt = now.AddMinutes(OtpExpiryMinutes),
            CreatedAt = now,
            IsUsed = false,
            Purpose = OtpPurpose.Login,
        });
        await _db.SaveChangesAsync();

        await _email.SendOtpEmailAsync(email, user.DisplayName, code);
    }

    // ── OTP: verify code ──────────────────────────────────────────────────────

    public async Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto request)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var codeHash = HashCode(request.Code.Trim());
        var now = DateTime.UtcNow;

        var otpRecord = await _db.OtpCodes
            .Where(o => o.Email == email
                     && o.Purpose == OtpPurpose.Login
                     && !o.IsUsed
                     && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord is null || otpRecord.CodeHash != codeHash)
            throw new UnauthorizedAccessException("Invalid or expired OTP code.");

        // Mark used
        otpRecord.IsUsed = true;
        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (user.DeletionScheduledAt is not null)
            throw new UnauthorizedAccessException("This account has been deleted.");

        await ReactivateIfNeededAsync(user);

        return await IssueTokensAsync(user);
    }

    // ── Google OAuth ──────────────────────────────────────────────────────────

    public async Task<AuthResponse> LoginWithGoogleAsync(string idToken)
    {
        var clientId = _config["OAuth:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google OAuth is not configured.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"Invalid Google token: {ex.Message}");
        }

        return await FindOrCreateOAuthUserAsync(
            email: payload.Email,
            displayName: payload.Name ?? payload.Email,
            avatarUrl: payload.Picture,
            provider: AuthProviderType.Google,
            providerUserId: payload.Subject);
    }

    // ── Facebook OAuth ────────────────────────────────────────────────────────

    public async Task<AuthResponse> LoginWithFacebookAsync(string accessToken)
    {
        var http = _httpClientFactory.CreateClient();
        var url = $"https://graph.facebook.com/me?access_token={Uri.EscapeDataString(accessToken)}&fields=id,name,email,picture";

        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Invalid Facebook access token.");

        var fbData = await response.Content.ReadFromJsonAsync<FacebookMeResponse>()
            ?? throw new UnauthorizedAccessException("Failed to read Facebook profile.");

        if (string.IsNullOrWhiteSpace(fbData.Id))
            throw new UnauthorizedAccessException("Facebook did not return a user ID.");

        return await FindOrCreateOAuthUserAsync(
            email: fbData.Email ?? $"fb_{fbData.Id}@facebook.local",
            displayName: fbData.Name ?? fbData.Id,
            avatarUrl: fbData.Picture?.Data?.Url,
            provider: AuthProviderType.Facebook,
            providerUserId: fbData.Id);
    }

    // ── Refresh tokens ────────────────────────────────────────────────────────

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var hash = TokenService.HashToken(refreshToken);
        var now = DateTime.UtcNow;

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && !rt.IsRevoked && rt.ExpiresAt > now);

        if (stored is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotate: revoke old, issue new
        stored.IsRevoked = true;
        await _db.SaveChangesAsync();

        return await IssueTokensAsync(stored.User);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    public async Task LogoutAsync(string refreshToken, string? accessToken = null)
    {
        // Revoke the refresh token in the database.
        var hash = TokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash);
        if (stored is not null)
        {
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();
        }

        // Blacklist the access token so it cannot be reused for the remainder
        // of its lifetime even though it is a stateless JWT.
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(accessToken))
                {
                    var parsed  = handler.ReadJwtToken(accessToken);
                    var jti     = parsed.Id;
                    var remaining = parsed.ValidTo.ToUniversalTime() - DateTime.UtcNow;

                    if (!string.IsNullOrEmpty(jti) && remaining > TimeSpan.Zero)
                        await _blacklist.BlacklistAsync(jti, remaining);
                }
            }
            catch
            {
                // Invalid / malformed token — nothing to blacklist.
            }
        }
    }

    // ── Password reset: request code ─────────────────────────────────────────

    public async Task RequestPasswordResetAsync(PasswordResetRequestDto request)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always return successfully — do not reveal whether the email exists.
        // Perform a dummy BCrypt hash when no user is found so that the response
        // time is indistinguishable from the happy path (email enumeration via timing).
        if (user is null)
        {
            BCrypt.Net.BCrypt.HashPassword("timing-pad-dummy-value");
            return;
        }

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);
        var now = DateTime.UtcNow;

        _db.OtpCodes.Add(new OtpCode
        {
            Id = Guid.NewGuid(),
            Email = email,
            CodeHash = codeHash,
            ExpiresAt = now.AddMinutes(PasswordResetExpiryMinutes),
            CreatedAt = now,
            IsUsed = false,
            Purpose = OtpPurpose.PasswordReset,
            AttemptCount = 0,
        });
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetEmailAsync(email, user.DisplayName, code);
    }

    // ── Password reset: confirm + set new password ────────────────────────────

    public async Task ConfirmPasswordResetAsync(PasswordResetConfirmDto request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            throw new ArgumentException("New password is required.");

        if (request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var email = request.Email.ToLowerInvariant().Trim();
        var codeHash = HashCode(request.Code.Trim());
        var now = DateTime.UtcNow;

        var otpRecord = await _db.OtpCodes
            .Where(o => o.Email == email
                     && o.Purpose == OtpPurpose.PasswordReset
                     && !o.IsUsed
                     && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord is null)
            throw new UnauthorizedAccessException("Invalid or expired reset code.");

        if (otpRecord.AttemptCount >= 5)
        {
            otpRecord.IsUsed = true;
            await _db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Too many attempts. Request a new code.");
        }

        if (otpRecord.CodeHash != codeHash)
        {
            otpRecord.AttemptCount++;
            await _db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Invalid or expired reset code.");
        }

        // Code matched — load the user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || user.DeletionScheduledAt is not null)
            throw new UnauthorizedAccessException("Invalid or expired reset code.");

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = now;

        // Mark the OTP as used
        otpRecord.IsUsed = true;

        // Revoke ALL active refresh tokens for this user
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync();
        foreach (var t in activeTokens)
            t.IsRevoked = true;

        // Advance the token-invalidation cutoff so any still-valid JWTs that
        // were issued BEFORE this moment are rejected by the middleware.
        user.TokenInvalidationCutoffUtc = now;

        await _db.SaveChangesAsync();

        // Push a security-event notification (persisted)
        await _notifications.CreateAsync(
            recipientUserId: user.Id,
            type: NotificationType.PasswordReset,
            referenceId: null,
            actorUserId: null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var accessToken = _tokens.GenerateAccessToken(user);
        var (rawRefresh, refreshHash) = _tokens.GenerateRefreshToken();
        var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 30);
        var now = DateTime.UtcNow;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = now.AddDays(expiryDays),
            CreatedAt = now,
            IsRevoked = false,
        });
        await _db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefresh,
            ExpiresIn: _tokens.AccessTokenExpirySeconds,
            User: MapUser(user)
        );
    }

    private async Task<AuthResponse> FindOrCreateOAuthUserAsync(
        string email,
        string displayName,
        string? avatarUrl,
        AuthProviderType provider,
        string providerUserId)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var now = DateTime.UtcNow;

        // Check if provider entry already exists
        var existingProvider = await _db.AuthProviders
            .Include(ap => ap.User)
            .FirstOrDefaultAsync(ap =>
                ap.Provider == provider && ap.ProviderUserId == providerUserId);

        if (existingProvider is not null)
        {
            if (existingProvider.User.DeletionScheduledAt is not null)
                throw new UnauthorizedAccessException("This account has been deleted.");
            await ReactivateIfNeededAsync(existingProvider.User);
            return await IssueTokensAsync(existingProvider.User);
        }

        // Try to find user by email (link existing account)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is not null)
        {
            if (user.DeletionScheduledAt is not null)
                throw new UnauthorizedAccessException("This account has been deleted.");
            await ReactivateIfNeededAsync(user);
        }

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                CreatedAt = now,
                UpdatedAt = now,
                PrivacySettings = new UserPrivacySettings(),
                NotificationPreferences = new NotificationPreferences()
            };
            user.PrivacySettings.UserId = user.Id;
            user.NotificationPreferences.UserId = user.Id;
            _db.Users.Add(user);
        }

        _db.AuthProviders.Add(new AuthProvider
        {
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = providerUserId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync();
        return await IssueTokensAsync(user);
    }

    private static UserDto MapUser(User u) => new(
        Id: u.Id,
        Email: u.Email,
        DisplayName: u.DisplayName,
        AvatarUrl: u.AvatarUrl,
        CoverUrl: u.CoverUrl,
        Bio: u.Bio,
        Gender: u.Gender.ToString(),
        BirthDate: u.BirthDate?.ToString("yyyy-MM-dd"),
        ShowBirthDate: u.ShowBirthDate,
        CreatedAt: u.CreatedAt,
        UrlSlug: u.UrlSlug,
        Role: u.Role.ToString(),
        LocationName: u.LocationName,
        LocationCity: u.LocationCity,
        LocationCountry: u.LocationCountry,
        IsEmailVerified: u.IsEmailVerified
    );

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    // ── Facebook DTO ──────────────────────────────────────────────────────────

    private sealed class FacebookMeResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public FacebookPicture? Picture { get; set; }
    }

    private sealed class FacebookPicture
    {
        public FacebookPictureData? Data { get; set; }
    }

    private sealed class FacebookPictureData
    {
        public string? Url { get; set; }
    }
}
