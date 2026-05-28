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
    Task LogoutAsync(string refreshToken);
}
