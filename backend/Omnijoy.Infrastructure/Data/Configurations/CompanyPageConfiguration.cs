using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class CompanyPageConfiguration : IEntityTypeConfiguration<CompanyPage>
{
    public void Configure(EntityTypeBuilder<CompanyPage> builder)
    {
        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.Id)
               .ValueGeneratedOnAdd();

        builder.Property(cp => cp.Name)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(cp => cp.Description)
               .HasColumnType("text");

        builder.Property(cp => cp.LogoUrl)
               .HasMaxLength(2048);

        builder.Property(cp => cp.CoverUrl)
               .HasMaxLength(2048);

        builder.Property(cp => cp.CreatedAt)
               .IsRequired();

        builder.HasIndex(cp => cp.CreatedByUserId);
        builder.HasIndex(cp => cp.Name);

        // Vanity URL slug — nullable + globally unique within the table
        // (cross-table uniqueness with Users.UrlSlug is enforced by ISlugService).
        builder.Property(cp => cp.UrlSlug)
               .HasMaxLength(30);

        builder.HasIndex(cp => cp.UrlSlug)
               .IsUnique();

        // ── Address fields ─────────────────────────────────────────────────────
        builder.Property(cp => cp.AddressPlaceId)
               .HasMaxLength(512);

        builder.Property(cp => cp.AddressText)
               .HasMaxLength(512);

        builder.Property(cp => cp.AddressCity)
               .HasMaxLength(256);

        builder.Property(cp => cp.AddressCountry)
               .HasMaxLength(256);

        builder.Property(cp => cp.AddressLatitude)
               .HasPrecision(9, 6);

        builder.Property(cp => cp.AddressLongitude)
               .HasPrecision(9, 6);

        builder.HasOne(cp => cp.CreatedByUser)
               .WithMany(u => u.CreatedCompanyPages)
               .HasForeignKey(cp => cp.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
