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
