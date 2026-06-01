using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/company-pages")]
[Authorize]
public class CompanyPagesController : ControllerBase
{
    private readonly ICompanyPageService _pages;
    private readonly ISlugService _slugs;

    public CompanyPagesController(ICompanyPageService pages, ISlugService slugs)
    {
        _pages = pages;
        _slugs = slugs;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/company-pages ───────────────────────────────────────────────

    /// <summary>
    /// Creates a new company page. Send as multipart/form-data.
    /// logo and cover are optional image file fields.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitConstants.UploadPolicy)]
    public async Task<IActionResult> CreatePage([FromForm] CreatePageFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var logo  = await ReadFileWithBracketFallbackAsync(input.Logo,  "Logo",  "logo");
            var cover = await ReadFileWithBracketFallbackAsync(input.Cover, "Cover", "cover");

            var request = new CreateCompanyPageRequest(
                Name:             input.Name ?? string.Empty,
                Description:      input.Description,
                AddressPlaceId:   input.AddressPlaceId,
                AddressText:      input.AddressText,
                AddressCity:      input.AddressCity,
                AddressCountry:   input.AddressCountry,
                AddressLatitude:  input.AddressLatitude,
                AddressLongitude: input.AddressLongitude
            );

            var page = await _pages.CreatePageAsync(userId, request, logo, cover);
            return Created($"/api/company-pages/{page.Id}", page);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── GET /api/company-pages ────────────────────────────────────────────────

    /// <summary>
    /// Lists company pages. mine=true returns only pages the current user administers.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPages(
        [FromQuery] bool mine     = false,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 20)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        var result = await _pages.GetPagesAsync(userId, mine, page, pageSize);
        return Ok(result);
    }

    // ── GET /api/company-pages/mine ───────────────────────────────────────────

    /// <summary>
    /// Returns all company pages where the current user has an admin role.
    /// Used by the post composer "Posting as" selector.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyPages()
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        var pages = await _pages.GetMyAdminPagesAsync(userId);
        return Ok(pages);
    }

    // ── GET /api/company-pages/:id ────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPage(Guid id)
    {
        try
        {
            var page = await _pages.GetPageAsync(id, CurrentUserId);
            return Ok(page);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── PUT /api/company-pages/:id ────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UpdatePage(Guid id, [FromForm] UpdatePageFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var logo  = await ReadFileWithBracketFallbackAsync(input.Logo,  "Logo",  "logo");
            var cover = await ReadFileWithBracketFallbackAsync(input.Cover, "Cover", "cover");

            var request = new UpdateCompanyPageRequest(
                Name:             input.Name,
                Description:      input.Description,
                AddressPlaceId:   input.AddressPlaceId,
                AddressText:      input.AddressText,
                AddressCity:      input.AddressCity,
                AddressCountry:   input.AddressCountry,
                AddressLatitude:  input.AddressLatitude,
                AddressLongitude: input.AddressLongitude
            );

            var page = await _pages.UpdatePageAsync(id, userId, request, logo, cover);
            return Ok(page);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── GET /api/company-pages/:id/admins ─────────────────────────────────────

    [HttpGet("{id:guid}/admins")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAdmins(Guid id)
    {
        try
        {
            var result = await _pages.GetAdminsAsync(id, CurrentUserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── POST /api/company-pages/:id/admins ────────────────────────────────────

    [HttpPost("{id:guid}/admins")]
    public async Task<IActionResult> AddAdmin(Guid id, [FromBody] AddAdminRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var result = await _pages.AddOrUpdateAdminAsync(id, userId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── DELETE /api/company-pages/:id/admins/:userId ──────────────────────────

    [HttpDelete("{id:guid}/admins/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveAdmin(Guid id, Guid targetUserId)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var result = await _pages.RemoveAdminAsync(id, userId, targetUserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── POST /api/company-pages/:id/follow ────────────────────────────────────

    [HttpPost("{id:guid}/follow")]
    public async Task<IActionResult> Follow(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var page = await _pages.FollowAsync(id, userId);
            return Ok(page);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── DELETE /api/company-pages/:id/follow ──────────────────────────────────

    [HttpDelete("{id:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var page = await _pages.UnfollowAsync(id, userId);
            return Ok(page);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── PUT /api/company-pages/{id}/slug ──────────────────────────────────────

    /// <summary>
    /// Sets or clears the company page's vanity URL slug. Caller must be an
    /// Owner or Admin of the page (Editor role is rejected). Body:
    /// <c>{ slug: string | null }</c>. Returns the canonical stored form.
    /// Note: the spec task lists this as <c>/api/companies/{id}/slug</c>; we
    /// keep the existing <c>/api/company-pages</c> base for consistency with
    /// every other endpoint on this resource.
    /// </summary>
    [HttpPut("{id:guid}/slug")]
    public async Task<IActionResult> SetSlug(Guid id, [FromBody] Omnijoy.Core.DTOs.Users.SetSlugRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var stored = await _slugs.SetCompanyPageSlugAsync(id, userId, request.Slug);
            return Ok(new { slug = stored });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "Slug is not available.", reason = ex.Message });
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<PageImageUploadItem?> ReadFileAsync(IFormFile? file, string kind)
    {
        if (file is not { Length: > 0 }) return null;
        var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        return new PageImageUploadItem(ms, file.FileName, file.ContentType, kind);
    }

    /// <summary>
    /// Like <see cref="ReadFileAsync"/> but also falls back to bracket-notation
    /// (e.g. <c>logo[]</c>) which ASP.NET Core model binding does not map to the
    /// property automatically.
    /// </summary>
    private async Task<PageImageUploadItem?> ReadFileWithBracketFallbackAsync(
        IFormFile? file, string fieldName, string kind)
    {
        var item = await ReadFileAsync(file, kind);
        if (item != null) return item;

        var bracketFile = Request.Form.Files.GetFiles($"{fieldName}[]")
            .FirstOrDefault(f => f.Length > 0);
        if (bracketFile == null) return null;

        var ms = new MemoryStream();
        await bracketFile.CopyToAsync(ms);
        ms.Position = 0;
        return new PageImageUploadItem(ms, bracketFile.FileName, bracketFile.ContentType, kind);
    }
}

// ── Form-data input models ────────────────────────────────────────────────────

public class CreatePageFormInput
{
    public string?    Name             { get; set; }
    public string?    Description      { get; set; }
    public IFormFile? Logo             { get; set; }
    public IFormFile? Cover            { get; set; }
    // ── Physical address ──────────────────────────────────────────────────────
    public string?    AddressPlaceId   { get; set; }
    public string?    AddressText      { get; set; }
    public string?    AddressCity      { get; set; }
    public string?    AddressCountry   { get; set; }
    public decimal?   AddressLatitude  { get; set; }
    public decimal?   AddressLongitude { get; set; }
}

public class UpdatePageFormInput
{
    public string?    Name             { get; set; }
    public string?    Description      { get; set; }
    public IFormFile? Logo             { get; set; }
    public IFormFile? Cover            { get; set; }
    // ── Physical address ──────────────────────────────────────────────────────
    public string?    AddressPlaceId   { get; set; }
    public string?    AddressText      { get; set; }
    public string?    AddressCity      { get; set; }
    public string?    AddressCountry   { get; set; }
    public decimal?   AddressLatitude  { get; set; }
    public decimal?   AddressLongitude { get; set; }
}
