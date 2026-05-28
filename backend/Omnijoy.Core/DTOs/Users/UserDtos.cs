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
    DateTime CreatedAt
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
