using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/company-pages")]
[Authorize]
public class CompanyPagesController : ControllerBase
{
    private readonly ICompanyPageService _pages;

    public CompanyPagesController(ICompanyPageService pages)
    {
        _pages = pages;
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
    public async Task<IActionResult> CreatePage([FromForm] CreatePageFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var logo  = await ReadFileAsync(input.Logo,  "logo");
            var cover = await ReadFileAsync(input.Cover, "cover");

            var request = new CreateCompanyPageRequest(
                Name:        input.Name ?? string.Empty,
                Description: input.Description
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
            var logo  = await ReadFileAsync(input.Logo,  "logo");
            var cover = await ReadFileAsync(input.Cover, "cover");

            var request = new UpdateCompanyPageRequest(
                Name:        input.Name,
                Description: input.Description
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

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<PageImageUploadItem?> ReadFileAsync(IFormFile? file, string kind)
    {
        if (file is not { Length: > 0 }) return null;
        var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        return new PageImageUploadItem(ms, file.FileName, file.ContentType, kind);
    }
}

// ── Form-data input models ────────────────────────────────────────────────────

public class CreatePageFormInput
{
    public string?    Name        { get; set; }
    public string?    Description { get; set; }
    public IFormFile? Logo        { get; set; }
    public IFormFile? Cover       { get; set; }
}

public class UpdatePageFormInput
{
    public string?    Name        { get; set; }
    public string?    Description { get; set; }
    public IFormFile? Logo        { get; set; }
    public IFormFile? Cover       { get; set; }
}
