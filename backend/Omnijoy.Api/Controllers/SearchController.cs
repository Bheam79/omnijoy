using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;

    public SearchController(ISearchService search)
    {
        _search = search;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── GET /api/search ───────────────────────────────────────────────────────

    /// <summary>
    /// Full-text search across users, posts, events, and company pages.
    /// <para>
    /// Query parameters:
    /// <list type="bullet">
    ///   <item><c>q</c> — the search query (required)</item>
    ///   <item><c>type</c> — "all" | "users" | "posts" | "events" | "companies" (default: "all")</item>
    ///   <item><c>page</c> — 1-based page number (default: 1)</item>
    ///   <item><c>pageSize</c> — items per page, 1–50 (default: 20)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Authentication is optional. Unauthenticated callers only see public (Everyone) posts and events.
    /// </para>
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string  type     = "all",
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var result = await _search.SearchAsync(CurrentUserId, q, type, page, pageSize);
        return Ok(result);
    }

    // ── GET /api/search/suggest ───────────────────────────────────────────────

    /// <summary>
    /// Light-weight auto-suggest endpoint backing the top-nav search dropdown.
    /// Returns up to 5 results per category (users, posts, events, companies) in
    /// a single round-trip. Equivalent to <c>GET /api/search?q=…&amp;type=all&amp;pageSize=5</c>
    /// but exposed as a stable, dedicated route so the client can debounce calls
    /// to it without depending on shared search-page query semantics.
    /// </summary>
    [HttpGet("suggest")]
    [AllowAnonymous]
    public async Task<IActionResult> Suggest([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var result = await _search.SearchAsync(CurrentUserId, q, type: "all", page: 1, pageSize: 5);
        return Ok(result);
    }
}
