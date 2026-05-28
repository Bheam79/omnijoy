using System.Security.Claims;
using Omnijoy.Core.Models;

namespace Omnijoy.Core.Interfaces;

public interface ITokenService
{
    /// <summary>Generates a signed JWT access token for the given user.</summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a secure refresh token.
    /// Returns the raw token (for sending to client) and its SHA-256 hash (for DB storage).
    /// </summary>
    (string token, string hash) GenerateRefreshToken();

    /// <summary>Returns the access token expiry in seconds (e.g. 3600).</summary>
    int AccessTokenExpirySeconds { get; }
}
