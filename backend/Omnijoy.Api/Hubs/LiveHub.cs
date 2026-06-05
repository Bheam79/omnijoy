using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Metrics;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Hub for live streaming events: stream start/end notifications,
/// viewer count updates, and live chat during a stream.
/// </summary>
[Authorize]
public class LiveHub : Hub
{
    // connectionId → streamId (tracks which stream each connection is watching)
    private static readonly ConcurrentDictionary<string, string> _connectionStream
        = new(StringComparer.Ordinal);

    // streamId → set of connectionIds
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _streamViewers
        = new(StringComparer.Ordinal);

    public override async Task OnConnectedAsync()
    {
        SignalRMetrics.Increment("live");

        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        SignalRMetrics.Decrement("live");

        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        // Clean up viewer tracking if this connection was watching a stream
        if (_connectionStream.TryRemove(Context.ConnectionId, out var streamId))
        {
            if (_streamViewers.TryGetValue(streamId, out var viewers))
            {
                viewers.TryRemove(Context.ConnectionId, out _);
                var count = viewers.Count;
                await Clients.Group($"stream:{streamId}")
                    .SendAsync("ViewerLeft", streamId, count);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Viewer joins a live stream room to receive chat + viewer count updates.</summary>
    public async Task JoinStream(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream:{streamId}");

        // Track the viewer
        _connectionStream[Context.ConnectionId] = streamId;
        var viewers = _streamViewers.GetOrAdd(streamId, _ => new ConcurrentDictionary<string, byte>());
        viewers[Context.ConnectionId] = 0;

        var count = viewers.Count;
        await Clients.Group($"stream:{streamId}")
            .SendAsync("ViewerJoined", streamId, count);
    }

    /// <summary>Viewer leaves a live stream room.</summary>
    public async Task LeaveStream(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream:{streamId}");

        _connectionStream.TryRemove(Context.ConnectionId, out _);
        if (_streamViewers.TryGetValue(streamId, out var viewers))
        {
            viewers.TryRemove(Context.ConnectionId, out _);
            var count = viewers.Count;
            await Clients.Group($"stream:{streamId}")
                .SendAsync("ViewerLeft", streamId, count);
        }
    }

    /// <summary>Send a chat message to all viewers of a stream.</summary>
    public async Task SendLiveChat(string streamId, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var userId = Context.UserIdentifier;
        await Clients.Group($"stream:{streamId}")
            .SendAsync("LiveChatMessage", new
            {
                streamId,
                userId,
                message = message.Trim(),
                sentAt = DateTime.UtcNow,
            });
    }

    /// <summary>Returns the current viewer count for a stream.</summary>
    public Task<int> GetViewerCount(string streamId)
    {
        var count = _streamViewers.TryGetValue(streamId, out var viewers)
            ? viewers.Count
            : 0;
        return Task.FromResult(count);
    }
}
