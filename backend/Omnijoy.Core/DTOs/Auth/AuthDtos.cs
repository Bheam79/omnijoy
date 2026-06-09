using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.DTOs.Auth;

// ── Requests ─────────────────────────────────────────────────────────────────

public record RegisterRequest(
    string Email,
    string DisplayName,
    /// <summary>"password" or "otp"</summary>
    string AuthMethod,
    string? Password,
    string Gender = "NotDisclosed",
    DateOnly? BirthDate = null,
    bool ShowBirthDate = false,
    /// <summary>Google Place ID for the user's home location.</summary>
    string? LocationPlaceId = null,
    /// <summary>Human-readable location label, e.g. "Oslo, Norway".</summary>
    string? LocationName = null,
    string? LocationCity = null,
    /// <summary>Required at registration — "Please select your location to continue".</summary>
    string? LocationCountry = null,
    string? LocationCountryCode = null,
    decimal? LocationLatitude = null,
    decimal? LocationLongitude = null
);

public record LoginPasswordRequest(string Email, string Password);

public record OtpRequestDto(string Email);

public record OtpVerifyDto(string Email, string Code);

/// <summary>
/// For Google: send the id_token obtained from Google Identity Services.
/// For Facebook: send the accessToken obtained from the Facebook JS SDK.
/// </summary>
public record OAuthRequest(string Token);

public record RefreshTokenRequest(string RefreshToken);

/// <summary>
/// Send <see cref="AccessToken"/> alongside <see cref="RefreshToken"/> so the
/// server can immediately blacklist the access token in Redis, preventing its
/// use for the remainder of its lifetime even though JWTs are stateless.
/// </summary>
public record LogoutRequest(string RefreshToken, string? AccessToken = null);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

// ── Password reset ─────────────────────────────────────────────────────────────

/// <summary>Step 1 of the password-reset flow: request a one-time code by email.</summary>
public class PasswordResetRequestDto
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Step 2 of the password-reset flow: verify the code and set the new password.</summary>
public class PasswordResetConfirmDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Body for POST /api/account/delete. The user must re-enter their email
/// address as a typing-confirmation step.
/// </summary>
public record DeleteAccountRequest(string ConfirmEmail);

// ── Responses ─────────────────────────────────────────────────────────────────

public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? CoverUrl,
    string? Bio,
    string Gender,
    string? BirthDate,
    bool ShowBirthDate,
    DateTime CreatedAt,
    /// <summary>Vanity URL slug — null when unset.</summary>
    string? UrlSlug = null,
    /// <summary>Platform role: "User" | "Moderator" | "Admin".</summary>
    string Role = "User",
    /// <summary>Human-readable location label, e.g. "Oslo, Norway".</summary>
    string? LocationName = null,
    string? LocationCity = null,
    string? LocationCountry = null,
    /// <summary>True once the user has confirmed their email address.</summary>
    bool IsEmailVerified = false
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    UserDto User
);
