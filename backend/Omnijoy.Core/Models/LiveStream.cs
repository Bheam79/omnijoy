using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class LiveStream
{
    public Guid Id { get; set; }
    public Guid HostUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public LiveStreamStatus Status { get; set; } = LiveStreamStatus.Scheduled;
    public string StreamKey { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    // Navigation property
    public User HostUser { get; set; } = null!;
}
