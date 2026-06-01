using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.DTOs.Live;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class LiveStreamService : ILiveStreamService
{
    private readonly OmnijoyDbContext _db;
    private readonly IConfiguration _config;

    public LiveStreamService(OmnijoyDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    public async Task<StartStreamResponse> StartStreamAsync(Guid userId, StartStreamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Stream title is required.");

        if (!Enum.TryParse<PrivacyLevel>(request.Privacy, ignoreCase: true, out var privacy))
            throw new ArgumentException($"Invalid Privacy value: '{request.Privacy}'. Use 'Everyone' or 'Friends'.");

        if (privacy is not (PrivacyLevel.Everyone or PrivacyLevel.Friends))
            throw new ArgumentException("Live streams must be either 'Everyone' (public) or 'Friends'.");

        // End any existing live stream for this user (there can only be one)
        var existing = await _db.LiveStreams
            .Where(ls => ls.HostUserId == userId && ls.Status == LiveStreamStatus.Live)
            .ToListAsync();
        foreach (var old in existing)
        {
            old.Status = LiveStreamStatus.Ended;
            old.EndedAt = DateTime.UtcNow;
        }

        var streamKey = GenerateStreamKey();

        var stream = new LiveStream
        {
            Id = Guid.NewGuid(),
            HostUserId = userId,
            Title = request.Title.Trim(),
            Privacy = privacy,
            Status = LiveStreamStatus.Live,
            StreamKey = streamKey,
            StartedAt = DateTime.UtcNow,
        };

        _db.LiveStreams.Add(stream);
        await _db.SaveChangesAsync();

        var ingestUrl = BuildIngestUrl(streamKey);
        var hlsUrl    = BuildHlsUrl(stream.Id);

        return new StartStreamResponse(
            stream.Id,
            stream.Title,
            streamKey,
            ingestUrl,
            hlsUrl
        );
    }

    // ── End ───────────────────────────────────────────────────────────────────

    public async Task EndStreamAsync(Guid streamId, Guid userId)
    {
        var stream = await _db.LiveStreams
            .FirstOrDefaultAsync(ls => ls.Id == streamId)
            ?? throw new KeyNotFoundException($"Live stream {streamId} not found.");

        if (stream.HostUserId != userId)
            throw new UnauthorizedAccessException("Only the host can end this stream.");

        if (stream.Status == LiveStreamStatus.Ended)
            return; // idempotent

        stream.Status = LiveStreamStatus.Ended;
        stream.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Active streams ────────────────────────────────────────────────────────

    public async Task<List<LiveStreamDto>> GetActiveStreamsAsync(Guid userId)
    {
        var friendIds = await GetFriendIdsAsync(userId);

        var streams = await _db.LiveStreams
            .AsNoTracking()
            .Include(ls => ls.HostUser)
            .Where(ls => ls.Status == LiveStreamStatus.Live &&
                         (ls.Privacy == PrivacyLevel.Everyone ||
                          (ls.Privacy == PrivacyLevel.Friends && friendIds.Contains(ls.HostUserId)) ||
                          ls.HostUserId == userId))
            .OrderByDescending(ls => ls.StartedAt)
            .ToListAsync();

        return streams.Select(s => MapToDto(s, 0)).ToList();
    }

    // ── Get single stream ─────────────────────────────────────────────────────

    public async Task<LiveStreamDto> GetStreamAsync(Guid streamId, Guid userId)
    {
        var stream = await _db.LiveStreams
            .AsNoTracking()
            .Include(ls => ls.HostUser)
            .FirstOrDefaultAsync(ls => ls.Id == streamId)
            ?? throw new KeyNotFoundException($"Live stream {streamId} not found.");

        await EnforceViewAccessAsync(stream, userId);

        return MapToDto(stream, 0);
    }

    // ── Stream key (for HLS proxy) ────────────────────────────────────────────

    public async Task<string?> GetStreamKeyAsync(Guid streamId)
    {
        return await _db.LiveStreams
            .AsNoTracking()
            .Where(ls => ls.Id == streamId)
            .Select(ls => (string?)ls.StreamKey)
            .FirstOrDefaultAsync();
    }

    // ── Stream key for browser ingest (host only, must be live) ──────────────

    public async Task<string> GetHostStreamKeyAsync(Guid streamId, Guid userId)
    {
        var stream = await _db.LiveStreams
            .AsNoTracking()
            .FirstOrDefaultAsync(ls => ls.Id == streamId)
            ?? throw new KeyNotFoundException($"Live stream {streamId} not found.");

        if (stream.HostUserId != userId)
            throw new UnauthorizedAccessException("Only the host can ingest to this stream.");

        if (stream.Status != LiveStreamStatus.Live)
            throw new InvalidOperationException("Stream is not currently live.");

        return stream.StreamKey;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public async Task<List<Guid>> GetFriendIdsAsync(Guid userId)
    {
        return await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
    }

    private async Task EnforceViewAccessAsync(LiveStream stream, Guid userId)
    {
        if (stream.HostUserId == userId) return; // host can always view

        bool canView = stream.Privacy switch
        {
            PrivacyLevel.Everyone => true,
            PrivacyLevel.Friends  => await _db.Friends.AnyAsync(f =>
                f.Status == FriendStatus.Accepted &&
                ((f.RequesterId == userId && f.AddresseeId == stream.HostUserId) ||
                 (f.RequesterId == stream.HostUserId && f.AddresseeId == userId))),
            _ => false,
        };

        if (!canView)
            throw new UnauthorizedAccessException("You do not have permission to view this stream.");
    }

    private LiveStreamDto MapToDto(LiveStream stream, int viewerCount)
    {
        var host = new LiveStreamHostDto(
            stream.HostUser.Id,
            stream.HostUser.DisplayName,
            stream.HostUser.AvatarUrl
        );

        var hlsUrl = stream.Status == LiveStreamStatus.Live
            ? BuildHlsUrl(stream.Id)
            : null;

        return new LiveStreamDto(
            stream.Id,
            host,
            stream.Title,
            stream.Status.ToString(),
            stream.Privacy.ToString(),
            viewerCount,
            hlsUrl,
            stream.StartedAt,
            stream.EndedAt
        );
    }

    // ── URL builders ──────────────────────────────────────────────────────────

    private string BuildIngestUrl(string streamKey)
    {
        var rtmpHost = _config["Live:RtmpHost"] ?? "localhost";
        var rtmpPort = _config["Live:RtmpPort"] ?? "1935";
        return $"rtmp://{rtmpHost}:{rtmpPort}/live/{streamKey}";
    }

    private string BuildHlsUrl(Guid streamId)
    {
        // Proxied HLS endpoint so the browser doesn't need direct access to MediaMTX
        return $"/api/live/{streamId}/hls/index.m3u8";
    }

    private static string GenerateStreamKey()
    {
        // 32 hex chars — URL-safe, hard to guess
        return Guid.NewGuid().ToString("N");
    }
}
