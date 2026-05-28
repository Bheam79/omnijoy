using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Events;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;
    private readonly IHubContext<FeedHub> _feedHub;
    private readonly INotificationService _notifications;

    public EventsController(
        IEventService events,
        IHubContext<FeedHub> feedHub,
        INotificationService notifications)
    {
        _events        = events;
        _feedHub       = feedHub;
        _notifications = notifications;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/events ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new event.
    /// Send as multipart/form-data or JSON; cover image as "coverImage" file field.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB cover image limit
    public async Task<IActionResult> CreateEvent([FromForm] CreateEventFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            CoverImageUploadItem? cover = null;
            if (input.CoverImage is { Length: > 0 })
            {
                var ms = new MemoryStream();
                await input.CoverImage.CopyToAsync(ms);
                ms.Position = 0;
                cover = new CoverImageUploadItem(ms, input.CoverImage.FileName, input.CoverImage.ContentType);
            }

            var request = new CreateEventRequest(
                Title:         input.Title ?? string.Empty,
                Description:   input.Description,
                StartAt:       input.StartAt,
                EndAt:         input.EndAt,
                Location:      input.Location,
                Privacy:       input.Privacy ?? "Everyone",
                CompanyPageId: input.CompanyPageId
            );

            var ev = await _events.CreateEventAsync(userId, request, cover);

            // ── Push EventCreated to friends via FeedHub ──────────────────────
            var friendIds = await _events.GetFriendIdsAsync(userId);
            foreach (var friendId in friendIds)
            {
                await _feedHub.Clients
                    .Group($"user:{friendId}")
                    .SendAsync("EventCreated", ev);
            }
            // Also push to creator so their own event view updates
            await _feedHub.Clients
                .Group($"user:{userId}")
                .SendAsync("EventCreated", ev);

            // ── Persist + push EventCreatedByFriend notifications to friends ──
            if (friendIds.Count > 0)
            {
                await _notifications.CreateForManyAsync(
                    recipientUserIds: friendIds,
                    type:             NotificationType.EventCreatedByFriend,
                    referenceId:      ev.Id.ToString(),
                    actorUserId:      userId);
            }

            return Created($"/api/events/{ev.Id}", ev);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── GET /api/events ───────────────────────────────────────────────────────

    /// <summary>
    /// Lists upcoming events visible to the current user.
    /// filter: "mine" | "friends" | "company" | (empty = all visible)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? filter   = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        var result = await _events.GetEventsAsync(userId, filter, page, pageSize);
        return Ok(result);
    }

    // ── GET /api/events/:id ───────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEvent(Guid id)
    {
        try
        {
            var ev = await _events.GetEventAsync(id, CurrentUserId);
            return Ok(ev);
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

    // ── PUT /api/events/:id ───────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromForm] UpdateEventFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            CoverImageUploadItem? cover = null;
            if (input.CoverImage is { Length: > 0 })
            {
                var ms = new MemoryStream();
                await input.CoverImage.CopyToAsync(ms);
                ms.Position = 0;
                cover = new CoverImageUploadItem(ms, input.CoverImage.FileName, input.CoverImage.ContentType);
            }

            var request = new UpdateEventRequest(
                Title:       input.Title,
                Description: input.Description,
                StartAt:     input.StartAt,
                EndAt:       input.EndAt,
                Location:    input.Location,
                Privacy:     input.Privacy
            );

            var ev = await _events.UpdateEventAsync(id, userId, request, cover);
            return Ok(ev);
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

    // ── DELETE /api/events/:id ────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            await _events.DeleteEventAsync(id, userId);
            return NoContent();
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

    // ── POST /api/events/:id/rsvp ─────────────────────────────────────────────

    [HttpPost("{id:guid}/rsvp")]
    public async Task<IActionResult> Rsvp(Guid id, [FromBody] RsvpRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var ev = await _events.RsvpAsync(id, userId, request.Status);
            return Ok(ev);
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

    // ── GET /api/events/:id/attendees ─────────────────────────────────────────

    [HttpGet("{id:guid}/attendees")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAttendees(Guid id)
    {
        try
        {
            var result = await _events.GetAttendeesAsync(id, CurrentUserId);
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
}

// ── Form-data input models ────────────────────────────────────────────────────

public class CreateEventFormInput
{
    public string?    Title         { get; set; }
    public string?    Description   { get; set; }
    public DateTime   StartAt       { get; set; }
    public DateTime?  EndAt         { get; set; }
    public string?    Location      { get; set; }
    public string?    Privacy       { get; set; }
    public Guid?      CompanyPageId { get; set; }
    public IFormFile? CoverImage    { get; set; }
}

public class UpdateEventFormInput
{
    public string?    Title       { get; set; }
    public string?    Description { get; set; }
    public DateTime?  StartAt     { get; set; }
    public DateTime?  EndAt       { get; set; }
    public string?    Location    { get; set; }
    public string?    Privacy     { get; set; }
    public IFormFile? CoverImage  { get; set; }
}
