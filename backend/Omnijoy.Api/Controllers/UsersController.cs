using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IFriendService _friends;
    private readonly IPresenceTracker _presence;
    private readonly ISlugService _slugs;

    public UsersController(
        IUserService users,
        IFriendService friends,
        IPresenceTracker presence,
        ISlugService slugs)
    {
        _users    = users;
        _friends  = friends;
        _presence = presence;
        _slugs    = slugs;
    }

    /// <summary>Returns the currently authenticated user's ID, or null if anonymous.</summary>
    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── GET /api/users/{id} ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a user's public profile. Anonymous requests are allowed;
    /// privacy settings are enforced server-side.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        try
        {
            var profile = await _users.GetUserProfileAsync(id, CurrentUserId);
            return Ok(profile);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    // ── PUT /api/users/me ─────────────────────────────────────────────────────

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var updated = await _users.UpdateProfileAsync(userId, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── POST /api/users/me/avatar ─────────────────────────────────────────────

    [HttpPost("me/avatar")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB request limit
    [EnableRateLimiting(RateLimitConstants.UploadPolicy)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        try
        {
            await using var stream = file.OpenReadStream();
            var updated = await _users.UploadAvatarAsync(userId, stream, file.FileName);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── POST /api/users/me/cover ──────────────────────────────────────────────

    [HttpPost("me/cover")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitConstants.UploadPolicy)]
    public async Task<IActionResult> UploadCover(IFormFile? file)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        try
        {
            await using var stream = file.OpenReadStream();
            var updated = await _users.UploadCoverAsync(userId, stream, file.FileName);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── GET /api/users/{id}/friends ───────────────────────────────────────────

    /// <summary>
    /// Returns the paginated friend list for a given user.
    /// Respects the target user's WhoCanSeeFriendList privacy setting.
    /// </summary>
    [HttpGet("{id:guid}/friends")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserFriends(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _friends.GetUserFriendsAsync(id, CurrentUserId, page, pageSize);
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
    }

    // ── GET /api/users/me/privacy ─────────────────────────────────────────────

    [HttpGet("me/privacy")]
    public async Task<IActionResult> GetPrivacy()
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        var settings = await _users.GetPrivacySettingsAsync(userId);
        return Ok(settings);
    }

    // ── GET /api/users/presence ───────────────────────────────────────────────

    /// <summary>
    /// Returns presence (online + last-seen) for a batch of user IDs.
    /// Use a comma-separated <c>userIds</c> query parameter.
    /// </summary>
    [HttpGet("presence")]
    public async Task<IActionResult> GetPresence([FromQuery] string? userIds)
    {
        if (CurrentUserId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(userIds)) return Ok(Array.Empty<object>());

        var ids = userIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToArray();

        var presence = await _presence.GetPresenceAsync(ids);
        return Ok(presence);
    }

    // ── PUT /api/users/me/slug ────────────────────────────────────────────────

    /// <summary>
    /// Sets or clears the authenticated user's vanity URL slug.
    /// Body: <c>{ slug: string | null }</c>. Returns the canonical stored form
    /// (lowercase) or null when cleared.
    /// </summary>
    [HttpPut("me/slug")]
    public async Task<IActionResult> SetSlug([FromBody] SetSlugRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var stored = await _slugs.SetUserSlugAsync(userId, request.Slug);
            return Ok(new { slug = stored });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // ex.Message carries the SlugUnavailableReason ("invalid"/"reserved"/"taken").
            return BadRequest(new { error = "Slug is not available.", reason = ex.Message });
        }
    }

    // ── PUT /api/users/me/privacy ─────────────────────────────────────────────

    [HttpPut("me/privacy")]
    public async Task<IActionResult> UpdatePrivacy([FromBody] UpdatePrivacyRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var updated = await _users.UpdatePrivacySettingsAsync(userId, request);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
