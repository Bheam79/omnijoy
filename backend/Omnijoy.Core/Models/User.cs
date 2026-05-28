using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? OtpSecret { get; set; }
    public Gender Gender { get; set; } = Gender.NotDisclosed;
    public DateOnly? BirthDate { get; set; }
    public bool ShowBirthDate { get; set; } = false;
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public UserPrivacySettings? PrivacySettings { get; set; }
    public ICollection<AuthProvider> AuthProviders { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Friend> SentFriendRequests { get; set; } = [];
    public ICollection<Friend> ReceivedFriendRequests { get; set; } = [];
    public ICollection<FamilyRelation> FamilyRelations { get; set; } = [];
    public ICollection<Event> CreatedEvents { get; set; } = [];
    public ICollection<EventAttendee> EventAttendances { get; set; } = [];
    public ICollection<CompanyPage> CreatedCompanyPages { get; set; } = [];
    public ICollection<CompanyPageAdmin> CompanyPageAdminships { get; set; } = [];
    public ICollection<CompanyPageFollow> CompanyPageFollows { get; set; } = [];
    public ICollection<ConversationParticipant> ConversationParticipants { get; set; } = [];
    public ICollection<Message> SentMessages { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<LiveStream> LiveStreams { get; set; } = [];
}
