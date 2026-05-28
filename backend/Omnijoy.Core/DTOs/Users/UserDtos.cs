namespace Omnijoy.Core.DTOs.Users;

// ── Profile responses ─────────────────────────────────────────────────────────

/// <summary>
/// Public-facing profile. Sensitive fields (BirthDate, email) are conditionally
/// included depending on who is requesting and the user's privacy settings.
/// </summary>
public record UserProfileDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string? CoverUrl,
    string? Bio,
    string Gender,
    /// <summary>Only present when ShowBirthDate is true OR viewer is the owner.</summary>
    string? BirthDate,
    bool ShowBirthDate,
    int FriendCount,
    int? MutualFriendCount,
    bool IsOwnProfile,
    bool IsFriend,
    DateTime CreatedAt,
    /// <summary>Vanity URL slug — null when unset; canonical lowercased form.</summary>
    string? UrlSlug = null
);

// ── Slug DTOs ─────────────────────────────────────────────────────────────────

/// <summary>Body for PUT /api/users/me/slug and /api/company-pages/{id}/slug.</summary>
public record SetSlugRequest(string? Slug);

/// <summary>Response for GET /api/slugs/check.</summary>
public record SlugAvailabilityDto(
    bool Available,
    /// <summary>"invalid" | "reserved" | "taken" — null when Available is true.</summary>
    string? Reason
);

/// <summary>Response for GET /api/slugs/resolve/{slug}.</summary>
public record SlugResolutionDto(
    /// <summary>"user" | "company"</summary>
    string Type,
    Guid Id
);

// ── Update requests ───────────────────────────────────────────────────────────

public record UpdateProfileRequest(
    string? DisplayName,
    string? Bio,
    string? Gender,
    string? BirthDate,      // "yyyy-MM-dd" or null to clear
    bool? ShowBirthDate
);

// ── Privacy DTOs ──────────────────────────────────────────────────────────────

public record PrivacySettingsDto(
    string WhoCanSeePosts,
    string WhoCanSeeProfile,
    string WhoCanSendMessages,
    string WhoCanSeeFriendList,
    string WhoCanSeeEvents,
    string WhoCanTagInPosts
);

public record UpdatePrivacyRequest(
    string? WhoCanSeePosts,
    string? WhoCanSeeProfile,
    string? WhoCanSendMessages,
    string? WhoCanSeeFriendList,
    string? WhoCanSeeEvents,
    string? WhoCanTagInPosts
);
