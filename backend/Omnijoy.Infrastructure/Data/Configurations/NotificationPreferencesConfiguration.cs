using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class NotificationPreferencesConfiguration : IEntityTypeConfiguration<NotificationPreferences>
{
    public void Configure(EntityTypeBuilder<NotificationPreferences> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(p => p.UserId);

        builder.HasOne(p => p.User)
               .WithOne(u => u.NotificationPreferences!)
               .HasForeignKey<NotificationPreferences>(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.LikesOnMyPosts).HasDefaultValue(true);
        builder.Property(p => p.CommentsOnMyPosts).HasDefaultValue(true);
        builder.Property(p => p.PostShares).HasDefaultValue(true);
        builder.Property(p => p.FriendRequests).HasDefaultValue(true);
        builder.Property(p => p.NewFollower).HasDefaultValue(true);
        builder.Property(p => p.Mentions).HasDefaultValue(true);
        builder.Property(p => p.NewPostsFromFriends).HasDefaultValue(true);
        builder.Property(p => p.FamilyRelations).HasDefaultValue(true);
        builder.Property(p => p.EventInvites).HasDefaultValue(true);
        builder.Property(p => p.DirectMessages).HasDefaultValue(true);
        builder.Property(p => p.LiveStreams).HasDefaultValue(true);
        builder.Property(p => p.CompanyPageInvites).HasDefaultValue(true);
    }
}
