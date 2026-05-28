using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
               .ValueGeneratedOnAdd();

        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
               .IsUnique();

        builder.Property(u => u.PasswordHash)
               .HasMaxLength(512);

        builder.Property(u => u.OtpSecret)
               .HasMaxLength(256);

        builder.Property(u => u.AvatarUrl)
               .HasMaxLength(2048);

        builder.Property(u => u.CoverUrl)
               .HasMaxLength(2048);

        builder.Property(u => u.Bio)
               .HasMaxLength(2000);

        builder.Property(u => u.Gender)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(u => u.CreatedAt)
               .IsRequired();

        builder.Property(u => u.UpdatedAt)
               .IsRequired();

        // One-to-one with UserPrivacySettings
        builder.HasOne(u => u.PrivacySettings)
               .WithOne(ps => ps.User)
               .HasForeignKey<UserPrivacySettings>(ps => ps.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
