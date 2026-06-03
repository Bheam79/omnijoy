using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TokenCutoffChecker.IsTokenStaleAsync"/>.
/// Validates the logic that rejects JWT access tokens issued before the
/// <see cref="User.TokenInvalidationCutoffUtc"/> that is stamped during a
/// password reset.
/// </summary>
public class TokenCutoffCheckerTests : IDisposable
{
    private readonly OmnijoyDbContext _db;

    public TokenCutoffCheckerTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(DateTime? cutoff = null)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id                          = Guid.NewGuid(),
            Email                       = $"u{Guid.NewGuid():N}@test.com",
            DisplayName                 = "Test",
            TokenInvalidationCutoffUtc  = cutoff,
            CreatedAt                   = now,
            UpdatedAt                   = now,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private static long ToUnixSeconds(DateTime utc)
        => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsTokenStaleAsync_NoCutoff_ReturnsFalse()
    {
        // No cutoff set — any token must be accepted.
        var user = await SeedUserAsync(cutoff: null);
        var iat  = ToUnixSeconds(DateTime.UtcNow.AddHours(-1));

        var result = await TokenCutoffChecker.IsTokenStaleAsync(_db, user.Id, iat);

        result.Should().BeFalse("no cutoff means the token is valid regardless of age");
    }

    [Fact]
    public async Task IsTokenStaleAsync_TokenIssuedBeforeCutoff_ReturnsTrue()
    {
        // Cutoff = now.  Token was issued 5 minutes ago → stale.
        var cutoff = DateTime.UtcNow;
        var user   = await SeedUserAsync(cutoff: cutoff);
        var iat    = ToUnixSeconds(cutoff.AddMinutes(-5));

        var result = await TokenCutoffChecker.IsTokenStaleAsync(_db, user.Id, iat);

        result.Should().BeTrue("token issued 5 minutes before the cutoff must be rejected");
    }

    [Fact]
    public async Task IsTokenStaleAsync_TokenIssuedAfterCutoff_ReturnsFalse()
    {
        // Cutoff = 10 minutes ago.  Token was issued 1 minute ago → fresh.
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var user   = await SeedUserAsync(cutoff: cutoff);
        var iat    = ToUnixSeconds(DateTime.UtcNow.AddMinutes(-1));

        var result = await TokenCutoffChecker.IsTokenStaleAsync(_db, user.Id, iat);

        result.Should().BeFalse("token issued after the cutoff must be accepted");
    }

    [Fact]
    public async Task IsTokenStaleAsync_TokenIssuedExactlyAtCutoff_ReturnsFalse()
    {
        // Edge case: iat == cutoff to the second → not strictly before → accepted.
        var cutoff = DateTime.UtcNow;
        var user   = await SeedUserAsync(cutoff: cutoff);
        // Use the same second (ToUnixTimeSeconds truncates sub-second precision).
        var iat    = ToUnixSeconds(cutoff);

        var result = await TokenCutoffChecker.IsTokenStaleAsync(_db, user.Id, iat);

        result.Should().BeFalse("a token issued at exactly the cutoff second is not stale");
    }

    [Fact]
    public async Task IsTokenStaleAsync_UnknownUserId_ReturnsFalse()
    {
        // When the user does not exist the query returns null → no cutoff → valid.
        var unknownId = Guid.NewGuid();
        var iat       = ToUnixSeconds(DateTime.UtcNow.AddHours(-1));

        var result = await TokenCutoffChecker.IsTokenStaleAsync(_db, unknownId, iat);

        result.Should().BeFalse("an unknown user ID has no cutoff so no rejection applies");
    }

    [Fact]
    public async Task IsTokenStaleAsync_CutoffMovedForward_RejectsOlderToken()
    {
        // Simulate a second password reset: cutoff is bumped again.
        var firstCutoff  = DateTime.UtcNow.AddMinutes(-30); // first reset 30 min ago
        var secondCutoff = DateTime.UtcNow.AddMinutes(-5);  // second reset 5 min ago
        var user         = await SeedUserAsync(cutoff: firstCutoff);

        // Advance the cutoff (second reset)
        user.TokenInvalidationCutoffUtc = secondCutoff;
        await _db.SaveChangesAsync();

        // Token issued between the two resets (20 min ago) → stale under second cutoff
        var iatBetween = ToUnixSeconds(DateTime.UtcNow.AddMinutes(-20));
        var stale = await TokenCutoffChecker.IsTokenStaleAsync(_db, user.Id, iatBetween);
        stale.Should().BeTrue("token issued before the second cutoff must be rejected");

        // Token issued after the second reset (1 min ago) → fresh
        var iatAfter = ToUnixSeconds(DateTime.UtcNow.AddMinutes(-1));
        var fresh = await TokenCutoffChecker.IsTokenStaleAsync(_db, user.Id, iatAfter);
        fresh.Should().BeFalse("token issued after the second cutoff must be accepted");
    }
}
