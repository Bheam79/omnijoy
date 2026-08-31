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
    /// <summary>
    /// Platform-level role. Defaults to <see cref="UserRole.User"/>.
    /// Only <see cref="UserRole.Admin"/> users may promote or demote others.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Vanity URL slug — lowercase a–z / 0–9 / hyphen / underscore, 3–30 chars.
    /// Null when unset. Must be globally unique across <c>Users.UrlSlug</c>
    /// AND <c>CompanyPages.UrlSlug</c> (enforced in <c>ISlugService</c>).
    /// </summary>
    public string? UrlSlug { get; set; }

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

    /// <summary>
    /// True if the account has been banned by a moderator/admin. Banned users
    /// cannot log in or take authenticated actions. Cleared by an explicit
    /// unban action; the ban history is preserved in <c>ModerationLogs</c>.
    /// </summary>
    public bool IsBanned { get; set; } = false;

    /// <summary>
    /// Timestamp the account was most recently banned, or <c>null</c> if it
    /// is not currently banned (cleared on unban).
    /// </summary>
    public DateTime? BannedAt { get; set; }

    /// <summary>
    /// When set, any JWT whose <c>iat</c> (issued-at) claim is earlier than
    /// this timestamp is rejected by the middleware. Stamped with
    /// <see cref="DateTime.UtcNow"/> during a confirmed password reset to
    /// invalidate all outstanding access tokens that pre-date the reset.
    /// Null when the account has never triggered a token invalidation.
    /// </summary>
    public DateTime? TokenInvalidationCutoffUtc { get; set; }

    /// <summary>
    /// True once the user has confirmed their email address via the
    /// verification link sent at registration. Defaults to false for newly
    /// created accounts.
    /// </summary>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>
    /// One-time token emailed to the user to confirm ownership of their
    /// email address. Cleared once the address is verified. Null when no
    /// verification is currently outstanding.
    /// </summary>
    public string? EmailVerificationToken { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Location ('where I'm from' — city/country level) ──────────────────────

    /// <summary>Google Place ID for the user's home location.</summary>
    public string? LocationPlaceId { get; set; }

    /// <summary>Human-readable display label, e.g. "Oslo, Norway".</summary>
    public string? LocationName { get; set; }

    /// <summary>City or town name.</summary>
    public string? LocationCity { get; set; }

    /// <summary>Country name.</summary>
    public string? LocationCountry { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "NO".</summary>
    public string? LocationCountryCode { get; set; }

    /// <summary>Latitude for distance comparisons (decimal degrees, WGS-84).</summary>
    public decimal? LocationLatitude { get; set; }

    /// <summary>Longitude for distance comparisons (decimal degrees, WGS-84).</summary>
    public decimal? LocationLongitude { get; set; }

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
    public ICollection<SavedPost> SavedPosts { get; set; } = [];
    public ICollection<SavedPostCollection> SavedPostCollections { get; set; } = [];
}
