using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.DTOs.Comments;

// ── Sub-records ───────────────────────────────────────────────────────────────

public record CommentAuthorDto(Guid Id, string DisplayName, string? AvatarUrl);

// ── Comment response ──────────────────────────────────────────────────────────

public record CommentDto(
    Guid Id,
    Guid PostId,
    CommentAuthorDto Author,
    Guid? ParentCommentId,
    string Content,
    int ReplyCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted,
    /// <summary>Total number of reactions on this comment.</summary>
    int ReactionsCount,
    /// <summary>Up to the three most-used reaction types, ordered by count.</summary>
    ReactionCountDto[] TopReactions,
    /// <summary>The authenticated requester's reaction, or null when they have none.</summary>
    string? MyReaction,
    /// <summary>Resolved mentions persisted for this content.</summary>
    MentionDto[]? Mentions = null
);

// ── Create / Update requests ──────────────────────────────────────────────────

public record CreateCommentRequest(
    string Content,
    Guid? ParentCommentId = null
);

public record UpdateCommentRequest(string Content);
