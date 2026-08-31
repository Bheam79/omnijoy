using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data;

public class OmnijoyDbContext : DbContext
{
    public OmnijoyDbContext(DbContextOptions<OmnijoyDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPrivacySettings> UserPrivacySettings => Set<UserPrivacySettings>();
    public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();
    public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<SharedPost> SharedPosts => Set<SharedPost>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<SavedPost> SavedPosts => Set<SavedPost>();
    public DbSet<SavedPostCollection> SavedPostCollections => Set<SavedPostCollection>();
    public DbSet<PostMention> PostMentions => Set<PostMention>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentMention> CommentMentions => Set<CommentMention>();
    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<FamilyRelation> FamilyRelations => Set<FamilyRelation>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
    public DbSet<CompanyPage> CompanyPages => Set<CompanyPage>();
    public DbSet<CompanyPageAdmin> CompanyPageAdmins => Set<CompanyPageAdmin>();
    public DbSet<CompanyPageFollow> CompanyPageFollows => Set<CompanyPageFollow>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageMedia> MessageMedia => Set<MessageMedia>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<LiveStream> LiveStreams => Set<LiveStream>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ModerationLog> ModerationLogs => Set<ModerationLog>();
    public DbSet<FriendInvite> FriendInvites => Set<FriendInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OmnijoyDbContext).Assembly);

        // ── UTC DateTime convention ───────────────────────────────────────────
        // Pomelo returns DateTime values from MySQL with Kind = Unspecified.
        // System.Text.Json then serialises them without a 'Z' suffix, so
        // JavaScript's `new Date("2024-06-15T12:30:00")` treats the value as
        // local time (e.g. CET) instead of UTC, shifting it by the UTC offset.
        // This converter forces Kind = Utc on every read so the serialiser
        // emits the 'Z', and JavaScript parses the time correctly.
        // On writes it normalises any Local-kind values to UTC before storing.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue && v.Value.Kind != DateTimeKind.Utc
                    ? v.Value.ToUniversalTime()
                    : v,
            v => v.HasValue
                    ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                    : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(utcNullableConverter);
            }
        }
    }
}
