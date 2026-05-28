using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Reports;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/reports ─────────────────────────────────────────────────────

    /// <summary>
    /// Submit a report against a Post, User, or Comment.
    /// Body: { "targetType": "Post"|"User"|"Comment", "targetId": guid, "reason": "Spam"|"Harassment"|"Misinformation"|"Nudity"|"Violence"|"Other", "notes": string? }
    /// </summary>
    [HttpPost("api/reports")]
    public async Task<IActionResult> SubmitReport([FromBody] SubmitReportRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var report = await _reports.SubmitReportAsync(userId, request);
            return Created($"/api/reports/{report.Id}", report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // ── GET /api/admin/reports ────────────────────────────────────────────────

    /// <summary>
    /// List all reports. Accessible to Moderators and Admins.
    /// Query params: status (Pending|Reviewed|Dismissed), type (Post|User|Comment), page, pageSize
    /// </summary>
    [HttpGet("api/admin/reports")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> ListReports(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _reports.ListReportsAsync(status, type, page, pageSize);
        return Ok(result);
    }

    // ── PATCH /api/admin/reports/{id} ─────────────────────────────────────────

    /// <summary>
    /// Update the status of a report. Accessible to Moderators and Admins.
    /// Body: { "status": "Reviewed"|"Dismissed" }
    /// </summary>
    [HttpPatch("api/admin/reports/{id:guid}")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> UpdateReportStatus(Guid id, [FromBody] UpdateReportStatusRequest request)
    {
        if (CurrentUserId is not { } actorId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var report = await _reports.UpdateStatusAsync(actorId, id, request);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
