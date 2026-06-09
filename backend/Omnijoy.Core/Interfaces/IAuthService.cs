using Omnijoy.Core.DTOs.Auth;

namespace Omnijoy.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginWithPasswordAsync(LoginPasswordRequest request);
    Task RequestOtpAsync(OtpRequestDto request);
    Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto request);
    Task<AuthResponse> LoginWithGoogleAsync(string idToken);
    Task<AuthResponse> LoginWithFacebookAsync(string accessToken);
    Task<AuthResponse> RefreshAsync(string refreshToken);
    /// <summary>
    /// Revokes the refresh token and, if <paramref name="accessToken"/> is supplied,
    /// blacklists its JTI in the distributed cache so it cannot be reused for the
    /// remainder of its lifetime.
    /// </summary>
    Task LogoutAsync(string refreshToken, string? accessToken = null);

    /// <summary>
    /// Step 1 of password reset: generates a one-time code and sends it to the
    /// user's email address. Always returns successfully — unknown emails are
    /// silently ignored to prevent enumeration.
    /// </summary>
    Task RequestPasswordResetAsync(PasswordResetRequestDto request);

    /// <summary>
    /// Step 2 of password reset: verifies the OTP code, updates the password,
    /// revokes all refresh tokens, and pushes a security-event notification.
    /// Does NOT issue new tokens — the user must log in again.
    /// </summary>
    Task ConfirmPasswordResetAsync(PasswordResetConfirmDto request);

    /// <summary>
    /// Verifies a password-method user's email using the token from the link
    /// emailed at registration. Marks the account verified and clears the token.
    /// Throws <see cref="UnauthorizedAccessException"/> for invalid or
    /// already-used tokens.
    /// </summary>
    Task VerifyEmailAsync(string token);

    /// <summary>
    /// Regenerates the verification token and resends the verification email
    /// for the authenticated user. Throws <see cref="InvalidOperationException"/>
    /// if the email is already verified or the user does not have a password
    /// auth provider (OTP / OAuth users are verified at sign-up).
    /// </summary>
    Task ResendVerificationEmailAsync(Guid userId);
}
