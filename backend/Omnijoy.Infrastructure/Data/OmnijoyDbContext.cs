using Microsoft.EntityFrameworkCore;
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
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Friend> Friends => Set<Friend>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OmnijoyDbContext).Assembly);
    }
}
