using Omnijoy.Core.DTOs.Live;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for live stream management: start/end streams,
/// listing active streams, and querying stream details.
/// </summary>
public interface ILiveStreamService
{
    /// <summary>
    /// Creates a new live stream session for the given user.
    /// Returns the stream credentials (key + ingest URL) so the host can
    /// configure OBS or a browser-based capture tool.
    /// </summary>
    Task<StartStreamResponse> StartStreamAsync(Guid userId, StartStreamRequest request);

    /// <summary>
    /// Marks a live stream as ended. Only the owning host may end the stream.
    /// Throws <see cref="KeyNotFoundException"/> if the stream does not exist.
    /// Throws <see cref="UnauthorizedAccessException"/> if the caller is not the host.
    /// </summary>
    Task EndStreamAsync(Guid streamId, Guid userId);

    /// <summary>
    /// Returns the list of currently live streams visible to <paramref name="userId"/>
    /// (public streams + friends' streams).
    /// </summary>
    Task<List<LiveStreamDto>> GetActiveStreamsAsync(Guid userId);

    /// <summary>
    /// Returns details for a single stream.
    /// Throws <see cref="KeyNotFoundException"/> if the stream does not exist.
    /// Throws <see cref="UnauthorizedAccessException"/> if the caller may not view it.
    /// </summary>
    Task<LiveStreamDto> GetStreamAsync(Guid streamId, Guid userId);

    /// <summary>
    /// Returns the accepted-friend user IDs for <paramref name="userId"/> —
    /// used to push <c>StreamStarted</c> SignalR events to the right connections.
    /// </summary>
    Task<List<Guid>> GetFriendIdsAsync(Guid userId);

    /// <summary>
    /// Returns the raw stream key for a given stream ID.
    /// Returns <c>null</c> if the stream doesn't exist.
    /// Used internally by the HLS proxy endpoint.
    /// </summary>
    Task<string?> GetStreamKeyAsync(Guid streamId);

    /// <summary>
    /// Returns the stream key for a live stream owned by <paramref name="userId"/>.
    /// Throws <see cref="KeyNotFoundException"/> if the stream does not exist.
    /// Throws <see cref="UnauthorizedAccessException"/> if the caller is not the host.
    /// Throws <see cref="InvalidOperationException"/> if the stream is not currently live.
    /// Used by the browser-based WebSocket ingest endpoint.
    /// </summary>
    Task<string> GetHostStreamKeyAsync(Guid streamId, Guid userId);
}
