using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Live;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/live")]
[Authorize]
public class LiveController : ControllerBase
{
    private readonly ILiveStreamService _live;
    private readonly IHubContext<LiveHub> _liveHub;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public LiveController(
        ILiveStreamService live,
        IHubContext<LiveHub> liveHub,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _live              = live;
        _liveHub           = liveHub;
        _httpClientFactory = httpClientFactory;
        _config            = config;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/live/start ──────────────────────────────────────────────────

    /// <summary>
    /// Start a new live stream session. Returns the stream key and ingest URL
    /// for OBS or browser-based capture, plus the viewer HLS URL.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartStream([FromBody] StartStreamRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var response = await _live.StartStreamAsync(userId, request);

            // Notify friends that the stream has started
            var friendIds = await _live.GetFriendIdsAsync(userId);
            foreach (var friendId in friendIds)
            {
                await _liveHub.Clients
                    .Group($"user:{friendId}")
                    .SendAsync("StreamStarted", new
                    {
                        streamId = response.Id,
                        title    = response.Title,
                        hostId   = userId,
                        hlsUrl   = response.HlsUrl,
                    });
            }

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── POST /api/live/{id}/end ───────────────────────────────────────────────

    /// <summary>End an active live stream. Only the host may call this.</summary>
    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> EndStream(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            await _live.EndStreamAsync(id, userId);

            // Notify all viewers that the stream has ended
            await _liveHub.Clients
                .Group($"stream:{id}")
                .SendAsync("StreamEnded", id);

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

    // ── GET /api/live/active ──────────────────────────────────────────────────

    /// <summary>List active live streams visible to the current user.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveStreams()
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        var streams = await _live.GetActiveStreamsAsync(userId);
        return Ok(streams);
    }

    // ── GET /api/live/{id} ────────────────────────────────────────────────────

    /// <summary>Get details for a single live stream (includes viewer page URL).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStream(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var stream = await _live.GetStreamAsync(id, userId);
            return Ok(stream);
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

    // ── GET /api/live/{id}/hls/{**path} ──────────────────────────────────────

    /// <summary>
    /// Proxy HLS content (playlist + segments) from MediaMTX so the browser
    /// never needs direct access to the media-server port.
    /// Enforces the same privacy rules as GET /api/live/{id}.
    /// </summary>
    [HttpGet("{id:guid}/hls/{**path}")]
    public async Task<IActionResult> ProxyHls(Guid id, string path)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        // Privacy / existence check
        try
        {
            await _live.GetStreamAsync(id, userId);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }

        // Resolve the stream key for MediaMTX path
        var streamKey = await _live.GetStreamKeyAsync(id);
        if (streamKey is null)
            return NotFound(new { error = "Stream key not found." });

        var hlsBase   = (_config["Live:HlsBaseUrl"] ?? "http://localhost:8888").TrimEnd('/');
        var targetUrl = $"{hlsBase}/live/{streamKey}/{path}";

        var client = _httpClientFactory.CreateClient("mediamtx");
        try
        {
            var upstream = await client.GetAsync(targetUrl, HttpContext.RequestAborted);

            if (!upstream.IsSuccessStatusCode)
                return StatusCode((int)upstream.StatusCode);

            var bytes       = await upstream.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);
            var contentType = upstream.Content.Headers.ContentType?.ToString()
                              ?? "application/octet-stream";

            return File(bytes, contentType);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { error = "Media server is currently unavailable." });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499); // Client closed request
        }
    }
}
