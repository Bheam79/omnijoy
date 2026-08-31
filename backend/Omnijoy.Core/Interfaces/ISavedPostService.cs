using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>Manages a user's private saved-post set.</summary>
public interface ISavedPostService
{
    /// <summary>
    /// Saves a currently visible post. Returns <c>true</c> only when a new row
    /// was inserted; concurrent and repeated saves return <c>false</c>.
    /// </summary>
    Task<bool> SaveAsync(Guid userId, Guid postId, Guid? collectionId = null);

    /// <summary>Removes a saved post, returning whether a row was removed.</summary>
    Task<bool> UnsaveAsync(Guid userId, Guid postId);

    Task<bool> IsSavedAsync(Guid userId, Guid postId);

    /// <summary>Loads all saved IDs from a supplied post-ID batch in one query.</summary>
    Task<HashSet<Guid>> GetSavedPostIdsAsync(Guid userId, IReadOnlyCollection<Guid> postIds);

    /// <summary>Returns visible saved posts ordered newest-first.</summary>
    Task<PagedResult<SavedPostDto>> GetSavedAsync(Guid userId, int page, int pageSize);
}
