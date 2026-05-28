namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Maintains a revocation list for JWT access tokens identified by their JTI claim.
/// Backed by IDistributedCache (Redis in production, in-memory in tests/dev).
///
/// Usage: on logout, call <see cref="BlacklistAsync"/> with the token's JTI and
/// remaining TTL. Every authenticated request should call <see cref="IsBlacklistedAsync"/>
/// (wired into JwtBearerEvents.OnTokenValidated in Program.cs).
/// </summary>
public interface ITokenBlacklist
{
    /// <summary>
    /// Adds a token JTI to the blacklist with a TTL equal to the token's
    /// remaining lifetime so entries are automatically evicted on expiry.
    /// </summary>
    Task BlacklistAsync(string jti, TimeSpan ttl);

    /// <summary>Returns <c>true</c> if the given JTI has been blacklisted.</summary>
    Task<bool> IsBlacklistedAsync(string jti);
}
