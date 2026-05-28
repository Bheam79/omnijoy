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

public record LogoutRequest(string RefreshToken);

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
