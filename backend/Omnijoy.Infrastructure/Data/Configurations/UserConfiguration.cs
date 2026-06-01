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

        // Vanity URL slug — nullable + globally unique (NULL slots are
        // permitted; MySQL/MariaDB treats NULL as not-equal in unique
        // constraints, so multiple users can have a null slug.)
        builder.Property(u => u.UrlSlug)
               .HasMaxLength(30);

        builder.HasIndex(u => u.UrlSlug)
               .IsUnique();

        builder.Property(u => u.DisplayName)
               .IsRequired()
               .HasMaxLength(128);

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

        builder.Property(u => u.Role)
               .HasConversion<string>()
               .HasMaxLength(32)
               .IsRequired()
               .HasDefaultValue(Omnijoy.Core.Models.Enums.UserRole.User);

        builder.Property(u => u.IsActive)
               .IsRequired()
               .HasDefaultValue(true);

        builder.Property(u => u.IsBanned)
               .IsRequired()
               .HasDefaultValue(false);

        builder.Property(u => u.CreatedAt)
               .IsRequired();

        builder.Property(u => u.UpdatedAt)
               .IsRequired();

        // ── Location fields ────────────────────────────────────────────────────
        builder.Property(u => u.LocationPlaceId)
               .HasMaxLength(512);

        builder.Property(u => u.LocationName)
               .HasMaxLength(512);

        builder.Property(u => u.LocationCity)
               .HasMaxLength(256);

        builder.Property(u => u.LocationCountry)
               .HasMaxLength(256);

        builder.Property(u => u.LocationCountryCode)
               .HasMaxLength(8);

        builder.Property(u => u.LocationLatitude)
               .HasPrecision(9, 6);

        builder.Property(u => u.LocationLongitude)
               .HasPrecision(9, 6);

        // One-to-one with UserPrivacySettings
        builder.HasOne(u => u.PrivacySettings)
               .WithOne(ps => ps.User)
               .HasForeignKey<UserPrivacySettings>(ps => ps.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
