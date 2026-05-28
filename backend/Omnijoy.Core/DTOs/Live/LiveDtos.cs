namespace Omnijoy.Core.DTOs.Live;

// ── Sub-records ───────────────────────────────────────────────────────────────

public record LiveStreamHostDto(Guid Id, string DisplayName, string? AvatarUrl);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record LiveStreamDto(
    Guid Id,
    LiveStreamHostDto Host,
    string Title,
    /// <summary>"Scheduled" | "Live" | "Ended"</summary>
    string Status,
    /// <summary>"Everyone" | "Friends"</summary>
    string Privacy,
    int ViewerCount,
    /// <summary>HLS stream URL for the viewer player (proxied via the API).</summary>
    string? HlsUrl,
    DateTime? StartedAt,
    DateTime? EndedAt
);

/// <summary>Response from POST /api/live/start — contains credentials the host needs.</summary>
public record StartStreamResponse(
    Guid Id,
    string Title,
    string StreamKey,
    /// <summary>RTMP URL to configure in OBS (or other streaming software).</summary>
    string IngestUrl,
    /// <summary>HLS URL for the viewer player (proxied via the API).</summary>
    string HlsUrl
);

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record StartStreamRequest(
    string Title,
    /// <summary>"Everyone" | "Friends"</summary>
    string Privacy
);
