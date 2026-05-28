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
}
