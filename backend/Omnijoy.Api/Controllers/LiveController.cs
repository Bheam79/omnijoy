using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Live;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/live")]
[Authorize]
public class LiveController : ControllerBase
{
    private readonly ILiveStreamService _live;
    private readonly IHubContext<LiveHub> _liveHub;
    private readonly INotificationService _notifications;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public LiveController(
        ILiveStreamService live,
        IHubContext<LiveHub> liveHub,
        INotificationService notifications,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _live              = live;
        _liveHub           = liveHub;
        _notifications     = notifications;
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

            // ── Persist + push LiveStreamStarted notifications to friends ─────
            if (friendIds.Count > 0)
            {
                await _notifications.CreateForManyAsync(
                    recipientUserIds: friendIds,
                    type:             NotificationType.LiveStreamStarted,
                    referenceId:      response.Id.ToString(),
                    actorUserId:      userId);
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

    // ── GET /api/live/{id}/ingest (WebSocket) ────────────────────────────────

    /// <summary>
    /// WebSocket endpoint for browser-based live stream ingestion.
    /// The browser sends binary WebM chunks (from MediaRecorder); this handler
    /// pipes them to an FFmpeg process that re-muxes to RTMP and pushes to MediaMTX,
    /// which then outputs the HLS stream that viewers watch.
    ///
    /// Authentication: pass the JWT as the <c>access_token</c> query parameter
    /// (browsers cannot set custom headers on WebSocket connections).
    /// </summary>
    [HttpGet("{id:guid}/ingest")]
    public async Task IngestStream(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
        {
            HttpContext.Response.StatusCode = 401;
            return;
        }

        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 426; // Upgrade Required
            await HttpContext.Response.WriteAsJsonAsync(new { error = "WebSocket connection required." });
            return;
        }

        string streamKey;
        try
        {
            streamKey = await _live.GetHostStreamKeyAsync(id, userId);
        }
        catch (KeyNotFoundException ex)
        {
            HttpContext.Response.StatusCode = 404;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message });
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            HttpContext.Response.StatusCode = 403;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message });
            return;
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = 409;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message });
            return;
        }

        var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();

        var rtmpHost = _config["Live:RtmpHost"] ?? "localhost";
        var rtmpPort = _config["Live:RtmpPort"] ?? "1935";
        var rtmpUrl  = $"rtmp://{rtmpHost}:{rtmpPort}/live/{streamKey}";

        var logger = HttpContext.RequestServices
            .GetRequiredService<ILogger<LiveController>>();

        await StreamToRtmpAsync(ws, rtmpUrl, logger, cancellationToken);
    }

    private static async Task StreamToRtmpAsync(
        WebSocket ws,
        string rtmpUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // FFmpeg: read webm from stdin, transcode to H.264/AAC, push to RTMP.
        // -fflags +genpts regenerates timestamps to smooth any gaps between chunks.
        // -preset veryfast / -tune zerolatency minimize encoding latency.
        var ffmpegArgs =
            "-loglevel warning " +
            "-f webm -i pipe:0 " +
            "-c:v libx264 -preset veryfast -tune zerolatency " +
            "-b:v 2000k -maxrate 2500k -bufsize 5000k -pix_fmt yuv420p " +
            "-c:a aac -b:a 128k -ar 44100 " +
            "-fflags +genpts " +
            "-f flv " +
            $"\"{rtmpUrl}\"";

        using var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = "ffmpeg",
                Arguments              = ffmpegArgs,
                RedirectStandardInput  = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            },
            EnableRaisingEvents = true,
        };

        try
        {
            ffmpeg.Start();
            logger.LogInformation("FFmpeg ingest started for RTMP target {RtmpUrl}", rtmpUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start FFmpeg for RTMP ingest");
            await ws.CloseAsync(WebSocketCloseStatus.InternalServerError,
                "Failed to start streaming process", CancellationToken.None);
            return;
        }

        // Drain FFmpeg stderr to prevent the process from blocking.
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await ffmpeg.StandardError.ReadLineAsync(cts.Token)) is not null)
                    logger.LogDebug("ffmpeg: {Line}", line);
            }
            catch { /* ignore */ }
        }, cts.Token);

        var buffer = new byte[65536]; // 64 KB per receive call
        bool unexpectedTermination = false;
        try
        {
            while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cts.Token);
                }
                catch (WebSocketException)
                {
                    break; // client disconnected
                }

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.Count > 0)
                {
                    await ffmpeg.StandardInput.BaseStream
                        .WriteAsync(buffer.AsMemory(0, result.Count), cts.Token);
                    // Flush so FFmpeg receives the data promptly.
                    await ffmpeg.StandardInput.BaseStream.FlushAsync(cts.Token);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            // FFmpeg exited abnormally (e.g. RTMP target unreachable) or the
            // stdin write failed because the process already closed.
            unexpectedTermination = true;
            logger.LogWarning(ex, "WebSocket ingest stream ended unexpectedly");
        }
        finally
        {
            await cts.CancelAsync();

            try { ffmpeg.StandardInput.Close(); }
            catch { /* ignore */ }

            try { await ffmpeg.WaitForExitAsync(CancellationToken.None); }
            catch { /* ignore */ }

            // An FFmpeg process that exits with a non-zero code (e.g. could not
            // connect to the RTMP server) is also an unexpected termination even
            // if the exception path was not triggered.
            bool ffmpegFailed = ffmpeg.HasExited && ffmpeg.ExitCode != 0;

            logger.LogInformation("FFmpeg ingest stopped for {RtmpUrl} (exit={ExitCode})",
                rtmpUrl, ffmpeg.HasExited ? ffmpeg.ExitCode : -1);

            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    // Signal InternalServerError (1011) when the termination was
                    // unexpected so the browser can surface a meaningful error to
                    // the host instead of silently showing "stopped".
                    var closeStatus = (unexpectedTermination || ffmpegFailed)
                        ? WebSocketCloseStatus.InternalServerError
                        : WebSocketCloseStatus.NormalClosure;
                    var closeDescription = (unexpectedTermination || ffmpegFailed)
                        ? "Streaming process terminated unexpectedly"
                        : "Stream ended";
                    await ws.CloseAsync(closeStatus, closeDescription, CancellationToken.None);
                }
                catch { /* ignore */ }
            }
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
