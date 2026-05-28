using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class UserPrivacySettings
{
    public Guid UserId { get; set; }
    public PrivacyLevel WhoCanSeePosts { get; set; } = PrivacyLevel.Friends;
    public PrivacyLevel WhoCanSeeProfile { get; set; } = PrivacyLevel.Everyone;
    public PrivacyLevel WhoCanSendMessages { get; set; } = PrivacyLevel.Friends;
    public PrivacyLevel WhoCanSeeFriendList { get; set; } = PrivacyLevel.Friends;
    public PrivacyLevel WhoCanSeeEvents { get; set; } = PrivacyLevel.Friends;
    public PrivacyLevel WhoCanTagInPosts { get; set; } = PrivacyLevel.Friends;

    // Navigation property
    public User User { get; set; } = null!;
}
