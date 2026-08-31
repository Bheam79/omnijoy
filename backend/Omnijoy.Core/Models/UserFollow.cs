namespace Omnijoy.Core.Models;

/// <summary>A directed user-to-user follow relationship.</summary>
public class UserFollow
{
    public Guid FollowerId { get; set; }
    public Guid FolloweeId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Follower { get; set; } = null!;
    public User Followee { get; set; } = null!;
}
