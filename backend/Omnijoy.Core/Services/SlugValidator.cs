using System.Collections.Frozen;

namespace Omnijoy.Core.Services;

/// <summary>
/// Outcome of attempting to validate a candidate slug.
/// </summary>
public enum SlugValidationResult
{
    Valid,
    /// <summary>Failed the format/length/character rules.</summary>
    Invalid,
    /// <summary>Matched a reserved word or a top-level Vue Router path.</summary>
    Reserved,
}

/// <summary>
/// Stateless validator for vanity-URL slugs.
///
/// Rules (kept in sync with the spec on OMNIJOY-39):
///   • Length 3–30 characters.
///   • Allowed characters: lowercase a–z, 0–9, hyphen, underscore.
///   • Must start with a letter.
///   • No two consecutive separators (--, __, -_, _-).
///   • Cannot end in a separator.
///   • Comparisons are case-insensitive; canonical storage form is lowercase.
///   • Cannot match a reserved word (system routes / API paths).
///
/// The reserved list is intentionally hard-coded here so the validator works
/// offline (no DB hit) and so the list is auditable in version control.
///
/// Vue Router top-level paths (router/index.ts) MUST be reflected in
/// <see cref="ReservedSlugs"/>. CLAUDE.md documents this constraint — when
/// adding a new top-level frontend route, update this list too.
/// </summary>
public static class SlugValidator
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    /// <summary>
    /// Hard-coded reserved word list. Includes:
    ///   • System concept words (admin, api, hubs, swagger, …)
    ///   • Auth flow paths (login, register, signup, logout, signin, signout, …)
    ///   • Every current top-level Vue Router path (see /frontend/src/router/index.ts):
    ///     home, login, register, wall, friends, profile, events, company, live,
    ///     search, settings, notifications, share.
    ///   • Aliases / plurals (companies, users, friend, event, …)
    /// </summary>
    public static readonly FrozenSet<string> ReservedSlugs =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Profile / pages
            "profile", "company", "companies", "user", "users",
            // Settings / auth
            "settings", "login", "register", "signup", "logout", "signin", "signout",
            "forgot-password",
            // Social surfaces
            "feed", "messenger", "chat", "conversations", "notifications", "search",
            "friends", "friend", "events", "event", "live", "wall", "share",
            // Static / system paths
            "admin", "api", "hubs", "hub",
            // Site meta
            "about", "terms", "privacy", "help", "support",
            // Misc reserved
            "home", "index", "404", "error", "invite", "invites",
            // Build/static
            "assets", "static", "public", "wwwroot", "swagger",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True iff <paramref name="raw"/> meets every format rule above AND is
    /// not in <see cref="ReservedSlugs"/>.
    /// </summary>
    public static SlugValidationResult Validate(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return SlugValidationResult.Invalid;

        // Canonical (lowercase) form for comparisons. Storage callers should
        // also persist the lowercase form via Normalize().
        var slug = raw.ToLowerInvariant();

        if (slug.Length < MinLength || slug.Length > MaxLength)
            return SlugValidationResult.Invalid;

        // Must start with a letter.
        if (!IsLetter(slug[0]))
            return SlugValidationResult.Invalid;

        // Cannot end in a separator.
        if (IsSeparator(slug[^1]))
            return SlugValidationResult.Invalid;

        for (int i = 0; i < slug.Length; i++)
        {
            var c = slug[i];
            if (!IsAllowedChar(c))
                return SlugValidationResult.Invalid;

            if (i > 0 && IsSeparator(c) && IsSeparator(slug[i - 1]))
                return SlugValidationResult.Invalid; // consecutive separators
        }

        if (ReservedSlugs.Contains(slug))
            return SlugValidationResult.Reserved;

        return SlugValidationResult.Valid;
    }

    /// <summary>
    /// Canonical storage form. Trims whitespace and lowercases the input.
    /// Returns null for null/blank input so callers can pass through "clear"
    /// requests transparently.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant();
    }

    private static bool IsLetter(char c)      => c >= 'a' && c <= 'z';
    private static bool IsDigit(char c)       => c >= '0' && c <= '9';
    private static bool IsSeparator(char c)   => c == '-' || c == '_';
    private static bool IsAllowedChar(char c) => IsLetter(c) || IsDigit(c) || IsSeparator(c);
}
