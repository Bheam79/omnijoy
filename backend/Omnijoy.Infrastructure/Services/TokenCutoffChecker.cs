using Microsoft.EntityFrameworkCore;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Determines whether a JWT access token predates the user's token-invalidation
/// cutoff (set during a password reset).  Thread-safe; resolves the cutoff with
/// a single projected DB query.
/// </summary>
public static class TokenCutoffChecker
{
    /// <summary>
    /// Returns <c>true</c> when the token must be rejected because its
    /// <c>iat</c> (issued-at) Unix timestamp predates the user's
    /// <see cref="Omnijoy.Core.Models.User.TokenInvalidationCutoffUtc"/>.
    /// Returns <c>false</c> when no cutoff is set or when the token is at least
    /// as recent as the cutoff.
    /// </summary>
    /// <param name="db">Scoped DB context resolved from the current request scope.</param>
    /// <param name="userId">Subject user ID parsed from the JWT <c>sub</c> claim.</param>
    /// <param name="issuedAtUnix">Unix epoch seconds from the JWT <c>iat</c> claim.</param>
    public static async Task<bool> IsTokenStaleAsync(
        OmnijoyDbContext db, Guid userId, long issuedAtUnix)
    {
        // Single projected query — only fetches the DateTime? column.
        var cutoff = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TokenInvalidationCutoffUtc)
            .FirstOrDefaultAsync();

        if (cutoff is null) return false; // No cutoff configured — token is valid.

        // Compare at Unix-second granularity.  The JWT iat claim is a whole-second
        // Unix timestamp, but the DB cutoff has sub-second precision.  Truncating
        // both to seconds avoids a false rejection when iat and cutoff fall in the
        // same second (e.g. cutoff = 12:34:56.789, iat = 12:34:56).
        var cutoffUnix = new DateTimeOffset(cutoff.Value).ToUnixTimeSeconds();
        return issuedAtUnix < cutoffUnix;
    }
}
