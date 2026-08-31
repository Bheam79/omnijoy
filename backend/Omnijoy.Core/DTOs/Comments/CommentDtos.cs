using Omnijoy.Core.DTOs;

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
    /// <summary>Resolved mentions persisted for this content.</summary>
    MentionDto[]? Mentions = null
);

// ── Create / Update requests ──────────────────────────────────────────────────

public record CreateCommentRequest(
    string Content,
    Guid? ParentCommentId = null
);

public record UpdateCommentRequest(string Content);
