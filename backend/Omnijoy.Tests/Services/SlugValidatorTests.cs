using FluentAssertions;
using Omnijoy.Core.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Format / length / character / reserved-word rules for vanity URL slugs.
/// Pure-function tests — no DB.
/// </summary>
public class SlugValidatorTests
{
    // ── Happy path ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("alice")]
    [InlineData("alice123")]
    [InlineData("alice-doe")]
    [InlineData("alice_doe")]
    [InlineData("alice-doe-99")]
    [InlineData("abc")]                       // min length
    [InlineData("abcdefghijabcdefghijabcdef0123")] // 30 chars exactly
    public void Validate_ValidSlugs_ReturnValid(string slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Valid);
    }

    // ── Length rules ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("a")]
    [InlineData("ab")]                                          // too short
    [InlineData("abcdefghijabcdefghijabcdefghij1")]             // 31 chars
    public void Validate_LengthViolations_ReturnInvalid(string? slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Invalid);
    }

    // ── Start-with-letter rule ────────────────────────────────────────────────

    [Theory]
    [InlineData("1abc")]
    [InlineData("9alice")]
    [InlineData("-alice")]
    [InlineData("_alice")]
    public void Validate_NonLetterStart_ReturnInvalid(string slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Invalid);
    }

    // ── Character set rule ────────────────────────────────────────────────────

    [Theory]
    [InlineData("alice!")]
    [InlineData("alice@b")]
    [InlineData("alice.b")]
    [InlineData("alice b")]                                     // space
    [InlineData("aliceß")]
    [InlineData("Älice")]
    public void Validate_InvalidCharacters_ReturnInvalid(string slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Invalid);
    }

    // ── No consecutive separators ────────────────────────────────────────────

    [Theory]
    [InlineData("ali--ce")]
    [InlineData("ali__ce")]
    [InlineData("ali-_ce")]
    [InlineData("ali_-ce")]
    public void Validate_ConsecutiveSeparators_ReturnInvalid(string slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Invalid);
    }

    // ── No trailing separator ────────────────────────────────────────────────

    [Theory]
    [InlineData("alice-")]
    [InlineData("alice_")]
    public void Validate_TrailingSeparator_ReturnInvalid(string slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Invalid);
    }

    // ── Case insensitivity ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Alice")]
    [InlineData("ALICE")]
    [InlineData("AlIcE")]
    public void Validate_UppercaseLetters_AcceptedAsValidAfterLowercasing(string slug)
    {
        // Per spec, comparisons are case-insensitive; uppercase passes
        // because Validate() lowercases internally before checking the
        // a–z range.
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Valid);
    }

    // ── Reserved words ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("profile")]
    [InlineData("Profile")]    // case-insensitive
    [InlineData("ADMIN")]
    [InlineData("api")]
    [InlineData("hubs")]
    [InlineData("login")]
    [InlineData("settings")]
    [InlineData("notifications")]
    [InlineData("company")]
    [InlineData("companies")]
    [InlineData("wall")]
    [InlineData("saved")]
    [InlineData("share")]
    [InlineData("swagger")]
    public void Validate_ReservedWords_ReturnReserved(string slug)
    {
        SlugValidator.Validate(slug).Should().Be(SlugValidationResult.Reserved);
    }

    [Fact]
    public void ReservedSlugs_ContainsKnownEntries()
    {
        // Sanity check — both the spec list and the live frontend routes.
        SlugValidator.ReservedSlugs.Should().Contain(new[]
        {
            "profile", "company", "companies", "user", "users",
            "settings", "login", "register", "signup", "logout",
            "signin", "signout", "feed", "messenger", "chat",
            "conversations", "notifications", "search", "admin",
            "api", "hubs", "hub", "friends", "friend", "events",
            "event", "live", "about", "terms", "privacy", "help",
            "support", "home", "index", "404", "error", "assets",
            "static", "public", "wwwroot", "swagger",
            // Vue Router top-level paths
            "wall", "saved", "share",
        });
    }

    // ── Normalize ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Alice", "alice")]
    [InlineData("  alice-doe  ", "alice-doe")]
    [InlineData("AlIcE_99", "alice_99")]
    public void Normalize_LowercasesAndTrims(string raw, string expected)
    {
        SlugValidator.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankInput_ReturnsNull(string? raw)
    {
        SlugValidator.Normalize(raw).Should().BeNull();
    }
}
