namespace Omnijoy.Core.DTOs.Posts;

/// <summary>Count of reactions of a specific type on a post.</summary>
public record ReactionCountDto(string ReactionType, int Count);

/// <summary>Full reaction summary for a post (counts + current user's reaction).</summary>
public record PostReactionsDto(
    ReactionCountDto[] Counts,
    int TotalCount,
    string? CurrentUserReaction
);

/// <summary>Request body for POST /api/posts/{id}/reactions.</summary>
public record AddOrUpdateReactionRequest(string ReactionType);

/// <summary>
/// Real-time event pushed via FeedHub to the <c>post:{postId}</c> group
/// whenever the reaction tally changes.
/// </summary>
public record ReactionCountsUpdatedEvent(Guid PostId, ReactionCountDto[] Counts, int TotalCount);

/// <summary>A single person who reacted to a post, returned by the "who reacted" endpoint.</summary>
public record ReactionWhoUserDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    bool IsFriend,
    string ReactionType
);

/// <summary>
/// Response for <c>GET /api/posts/{id}/reactions/who</c>.
/// Up to 5 reactors (friends first), plus the count of those not listed.
/// </summary>
public record ReactionWhoDto(
    ReactionWhoUserDto[] People,
    int Remaining
);
