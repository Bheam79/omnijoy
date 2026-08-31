namespace Omnijoy.Core.DTOs;

/// <summary>
/// A mention that was resolved and persisted when content was written.
/// <paramref name="MatchedSlug"/> identifies the source handle in the content,
/// while <paramref name="UrlSlug"/> is the target user's current vanity slug.
/// </summary>
public record MentionDto(
    string MatchedSlug,
    Guid UserId,
    string DisplayName,
    string? UrlSlug
);
