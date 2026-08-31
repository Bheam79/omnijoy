namespace Omnijoy.Core.DTOs.Posts;

/// <summary>Collection metadata embedded in a saved-post result.</summary>
public record SavedPostCollectionDto(Guid Id, string Name);

/// <summary>A saved post together with its private bookmark metadata.</summary>
public record SavedPostDto(
    Guid Id,
    PostDto Post,
    SavedPostCollectionDto? Collection,
    DateTime SavedAt);
