namespace Omnijoy.Core.DTOs.Friends;

// ── Shared sub-records ────────────────────────────────────────────────────────

public record FriendUserDto(Guid Id, string DisplayName, string? AvatarUrl);

public record FamilyRelationDto(string RelationType, string Subtype);

// ── Friends list ──────────────────────────────────────────────────────────────

public record FriendDto(
    Guid FriendshipId,
    FriendUserDto User,
    int MutualFriendCount,
    DateTime FriendedAt,
    FamilyRelationDto? FamilyRelation
);

// ── Requests ──────────────────────────────────────────────────────────────────

public record FriendRequestDto(
    Guid FriendshipId,
    FriendUserDto From,
    DateTime SentAt
);

// ── Suggestions ───────────────────────────────────────────────────────────────

public record FriendSuggestionDto(
    FriendUserDto User,
    int MutualFriendCount
);

// ── Relationship status (for profile Add Friend button) ───────────────────────

/// <summary>
/// None | RequestSent | RequestReceived | Accepted | BlockedByMe | BlockedByThem
/// </summary>
public record FriendStatusDto(string Status);

// ── Family relation request ───────────────────────────────────────────────────

/// <summary>
/// Sets a family relation from the caller to the target user.
/// RelationType: "Parent" | "Sibling" | "Child"
/// Subtype: "Mother" | "Father" | "Sister" | "Brother" | "Cousin" | etc.
///          Pass null to clear the relation.
/// </summary>
public record SetFamilyRelationRequest(
    string? RelationType,
    string? Subtype
);
