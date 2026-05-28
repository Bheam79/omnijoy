using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class UserPrivacySettingsConfiguration : IEntityTypeConfiguration<UserPrivacySettings>
{
    public void Configure(EntityTypeBuilder<UserPrivacySettings> builder)
    {
        builder.HasKey(ps => ps.UserId);

        builder.Property(ps => ps.WhoCanSeePosts)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ps => ps.WhoCanSeeProfile)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ps => ps.WhoCanSendMessages)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ps => ps.WhoCanSeeFriendList)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ps => ps.WhoCanSeeEvents)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ps => ps.WhoCanTagInPosts)
               .HasConversion<string>()
               .HasMaxLength(32);
    }
}
