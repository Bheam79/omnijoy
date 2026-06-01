using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Search;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly OmnijoyDbContext _db;

    public SearchService(OmnijoyDbContext db)
    {
        _db = db;
    }

    public async Task<SearchResponse> SearchAsync(
        Guid?  requesterId,
        string query,
        string type,
        int    page,
        int    pageSize)
    {
        var q = query.Trim();

        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        if (string.IsNullOrWhiteSpace(q))
        {
            return new SearchResponse(q, type,
                [], [], [], [],
                page, pageSize, HasMore: false);
        }

        // Lowercase query so Contains() is case-insensitive for the EF InMemory provider
        // (MySQL LIKE is already case-insensitive with the default ci collation).
        var ql             = q.ToLowerInvariant();
        var normalizedType = type.Trim().ToLowerInvariant();

        // Pre-compute friend IDs once for authenticated users
        var friendIds = requesterId.HasValue
            ? await GetFriendIdsAsync(requesterId.Value)
            : new HashSet<Guid>();

        return normalizedType switch
        {
            "users"     => await SearchUsersPagedAsync(q, ql, page, pageSize),
            "posts"     => await SearchPostsPagedAsync(q, ql, page, pageSize, requesterId, friendIds),
            "events"    => await SearchEventsPagedAsync(q, ql, page, pageSize, requesterId, friendIds),
            "companies" => await SearchCompaniesPagedAsync(q, ql, page, pageSize),
            _           => await SearchAllAsync(q, ql, page, pageSize, requesterId, friendIds),
        };
    }

    // ── "all" mode: return a page of each category independently ─────────────

    private async Task<SearchResponse> SearchAllAsync(
        string q, string ql, int page, int pageSize,
        Guid?  requesterId, HashSet<Guid> friendIds)
    {
        var (users,     usersMore)     = await QueryUsersAsync(ql, page, pageSize);
        var (posts,     postsMore)     = await QueryPostsAsync(ql, page, pageSize, requesterId, friendIds);
        var (events,    eventsMore)    = await QueryEventsAsync(ql, page, pageSize, requesterId, friendIds);
        var (companies, companiesMore) = await QueryCompaniesAsync(ql, page, pageSize);

        // hasMore is true if any category has additional results
        var hasMore = usersMore || postsMore || eventsMore || companiesMore;

        return new SearchResponse(q, "all",
            users, posts, events, companies,
            page, pageSize, hasMore);
    }

    // ── Per-type paged searches ───────────────────────────────────────────────

    private async Task<SearchResponse> SearchUsersPagedAsync(
        string q, string ql, int page, int pageSize)
    {
        var (users, hasMore) = await QueryUsersAsync(ql, page, pageSize);
        return new SearchResponse(q, "users", users, [], [], [], page, pageSize, hasMore);
    }

    private async Task<SearchResponse> SearchPostsPagedAsync(
        string q, string ql, int page, int pageSize,
        Guid?  requesterId, HashSet<Guid> friendIds)
    {
        var (posts, hasMore) = await QueryPostsAsync(ql, page, pageSize, requesterId, friendIds);
        return new SearchResponse(q, "posts", [], posts, [], [], page, pageSize, hasMore);
    }

    private async Task<SearchResponse> SearchEventsPagedAsync(
        string q, string ql, int page, int pageSize,
        Guid?  requesterId, HashSet<Guid> friendIds)
    {
        var (events, hasMore) = await QueryEventsAsync(ql, page, pageSize, requesterId, friendIds);
        return new SearchResponse(q, "events", [], [], events, [], page, pageSize, hasMore);
    }

    private async Task<SearchResponse> SearchCompaniesPagedAsync(
        string q, string ql, int page, int pageSize)
    {
        var (companies, hasMore) = await QueryCompaniesAsync(ql, page, pageSize);
        return new SearchResponse(q, "companies", [], [], [], companies, page, pageSize, hasMore);
    }

    // ── Core query helpers ────────────────────────────────────────────────────

    private async Task<(UserSearchResult[] Results, bool HasMore)> QueryUsersAsync(
        string ql, int page, int pageSize)
    {
        var skip  = (page - 1) * pageSize;
        var fetch = pageSize + 1;   // one extra to detect HasMore without a separate COUNT

        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Where(u => u.DisplayName.ToLower().Contains(ql) ||
                        (u.Bio != null && u.Bio.ToLower().Contains(ql)))
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Id)
            .Skip(skip)
            .Take(fetch)
            .Select(u => new UserSearchResult(u.Id, u.DisplayName, u.AvatarUrl, u.Bio))
            .ToArrayAsync();

        var hasMore = rows.Length > pageSize;
        return (rows.Take(pageSize).ToArray(), hasMore);
    }

    private async Task<(PostSearchResult[] Results, bool HasMore)> QueryPostsAsync(
        string ql, int page, int pageSize,
        Guid?  requesterId, HashSet<Guid> friendIds)
    {
        var skip  = (page - 1) * pageSize;
        var fetch = pageSize + 1;

        // Privacy rules:
        //  - Everyone posts  → visible to all (guests + authenticated)
        //  - Friends posts   → visible to the author + their accepted friends
        //  - OnlyMe posts    → visible to the author only
        var rows = await _db.Posts
            .AsNoTracking()
            .Where(p => p.DeletedAt == null)
            .Where(p => p.EventId == null)   // exclude event-wall posts from search
            .Where(p => p.Content.ToLower().Contains(ql))
            .Where(p =>
                p.Privacy == PrivacyLevel.Everyone ||
                (requesterId.HasValue && (
                    p.AuthorUserId == requesterId.Value ||
                    (friendIds.Contains(p.AuthorUserId) && p.Privacy != PrivacyLevel.OnlyMe)
                ))
            )
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(fetch)
            .Select(p => new PostSearchResult(
                p.Id,
                p.AuthorUserId,
                p.Author.DisplayName,
                p.Author.AvatarUrl,
                p.Content,
                p.PostType.ToString(),
                p.Privacy.ToString(),
                p.CreatedAt))
            .ToArrayAsync();

        var hasMore = rows.Length > pageSize;
        return (rows.Take(pageSize).ToArray(), hasMore);
    }

    private async Task<(EventSearchResult[] Results, bool HasMore)> QueryEventsAsync(
        string ql, int page, int pageSize,
        Guid?  requesterId, HashSet<Guid> friendIds)
    {
        var skip  = (page - 1) * pageSize;
        var fetch = pageSize + 1;

        var rows = await _db.Events
            .AsNoTracking()
            .Where(e => e.Title.ToLower().Contains(ql) ||
                        (e.Description != null && e.Description.ToLower().Contains(ql)))
            .Where(e =>
                e.Privacy == PrivacyLevel.Everyone ||
                (requesterId.HasValue && (
                    e.CreatorUserId == requesterId.Value ||
                    (friendIds.Contains(e.CreatorUserId) &&
                     (e.Privacy == PrivacyLevel.Friends || e.Privacy == PrivacyLevel.FriendsOfFriends))
                ))
            )
            .OrderByDescending(e => e.StartAt)
            .ThenBy(e => e.Id)
            .Skip(skip)
            .Take(fetch)
            .Select(e => new EventSearchResult(
                e.Id,
                e.Title,
                e.Description,
                e.StartAt,
                e.EndAt,
                e.Location,
                e.CoverImageUrl,
                e.CreatorUserId,
                e.CreatorUser.DisplayName,
                e.CompanyPageId))
            .ToArrayAsync();

        var hasMore = rows.Length > pageSize;
        return (rows.Take(pageSize).ToArray(), hasMore);
    }

    private async Task<(CompanySearchResult[] Results, bool HasMore)> QueryCompaniesAsync(
        string ql, int page, int pageSize)
    {
        var skip  = (page - 1) * pageSize;
        var fetch = pageSize + 1;

        var rows = await _db.CompanyPages
            .AsNoTracking()
            .Where(cp => cp.Name.ToLower().Contains(ql) ||
                         (cp.Description != null && cp.Description.ToLower().Contains(ql)))
            .OrderBy(cp => cp.Name)
            .ThenBy(cp => cp.Id)
            .Skip(skip)
            .Take(fetch)
            .Select(cp => new CompanySearchResult(
                cp.Id,
                cp.Name,
                cp.Description,
                cp.LogoUrl,
                cp.Followers.Count))
            .ToArrayAsync();

        var hasMore = rows.Length > pageSize;
        return (rows.Take(pageSize).ToArray(), hasMore);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private async Task<HashSet<Guid>> GetFriendIdsAsync(Guid userId)
    {
        var ids = await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        return ids.ToHashSet();
    }
}
