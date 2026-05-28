using Omnijoy.Core.DTOs.Search;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Full-text search across users, posts, events, and company pages.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches the platform for the given <paramref name="query"/>.
    /// <para>
    /// <paramref name="type"/> controls which entity category is searched:
    /// "all" returns results from every category (up to <paramref name="pageSize"/> per
    /// category on page 1); a specific value ("users" | "posts" | "events" | "companies")
    /// returns only that category's results with full pagination support.
    /// </para>
    /// <para>
    /// Post privacy is enforced: guests only see <c>Everyone</c> posts;
    /// authenticated users also see posts from accepted friends.
    /// </para>
    /// </summary>
    Task<SearchResponse> SearchAsync(
        Guid?  requesterId,
        string query,
        string type,
        int    page,
        int    pageSize);
}
