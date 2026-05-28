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
    public bool IsAdmin { get; set; } = false;

    /// <summary>
    /// True while the account is usable. Set to false on deactivation; logging
    /// back in re-activates it. Soft-deleted accounts also have IsActive=false.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the user voluntarily deactivated the account. Cleared
    /// on re-activation (successful login). Null when never deactivated.
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Timestamp when the user requested permanent deletion. The account
    /// remains soft-deleted for a configurable grace period before being
    /// purged. Null when the account is not pending deletion.
    /// </summary>
    public DateTime? DeletionScheduledAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public UserPrivacySettings? PrivacySettings { get; set; }
    public NotificationPreferences? NotificationPreferences { get; set; }
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
    public ICollection<Report> FiledReports { get; set; } = [];
}
