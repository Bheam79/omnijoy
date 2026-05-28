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
    bool ShowBirthDate = false
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
    DateTime CreatedAt
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    UserDto User
);
