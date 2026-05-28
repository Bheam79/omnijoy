using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

/// <summary>
/// Anonymous lookup + validation endpoints for vanity URL slugs.
/// Authenticated set/clear endpoints live on UsersController / CompanyPagesController.
/// </summary>
[ApiController]
[Route("api/slugs")]
public class SlugsController : ControllerBase
{
    private readonly ISlugService _slugs;

    public SlugsController(ISlugService slugs) => _slugs = slugs;

    // ── GET /api/slugs/check?slug=<value> ─────────────────────────────────────

    /// <summary>
    /// Returns <c>{ available: bool, reason?: "invalid" | "reserved" | "taken" }</c>
    /// for a candidate slug. Anonymous; rate-limiting is the responsibility
    /// of the global limiter (see OMNIJOY-37 for a dedicated policy).
    /// </summary>
    [HttpGet("check")]
    [AllowAnonymous]
    public async Task<IActionResult> Check([FromQuery] string? slug)
    {
        var avail = await _slugs.CheckAsync(slug);
        return Ok(new SlugAvailabilityDto(
            Available: avail.Available,
            Reason: avail.Reason is null ? null : avail.Reason.Value.ToString().ToLowerInvariant()));
    }

    // ── GET /api/slugs/resolve/{slug} ─────────────────────────────────────────

    /// <summary>
    /// Looks up the owner of a slug. Public profiles are public, so this is
    /// anonymous-callable; downstream profile/company endpoints still enforce
    /// their own visibility rules.
    /// </summary>
    [HttpGet("resolve/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> Resolve(string slug)
    {
        var owner = await _slugs.ResolveAsync(slug);
        if (owner is null)
            return NotFound(new { error = $"Slug '{slug}' is not assigned." });

        return Ok(new SlugResolutionDto(
            Type: owner.Type.ToString().ToLowerInvariant(),
            Id: owner.Id));
    }
}
