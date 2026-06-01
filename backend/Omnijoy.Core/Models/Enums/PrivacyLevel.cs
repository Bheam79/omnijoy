namespace Omnijoy.Core.Models.Enums;

public enum PrivacyLevel
{
    Everyone = 0,
    FriendsOfFriends = 1,
    Friends = 2,
    OnlyMe = 3,
    /// <summary>
    /// Company-page posts only: visible to followers of the company page.
    /// Cannot be used on personal posts.
    /// </summary>
    Followers = 4,
}
