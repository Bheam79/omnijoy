using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class AuthProviderConfiguration : IEntityTypeConfiguration<AuthProvider>
{
    public void Configure(EntityTypeBuilder<AuthProvider> builder)
    {
        builder.HasKey(ap => ap.Id);

        builder.Property(ap => ap.Provider)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ap => ap.ProviderUserId)
               .HasMaxLength(256);

        builder.Property(ap => ap.CreatedAt)
               .IsRequired();

        // Composite unique index: one provider entry per user per provider type
        builder.HasIndex(ap => new { ap.UserId, ap.Provider })
               .IsUnique();

        builder.HasOne(ap => ap.User)
               .WithMany(u => u.AuthProviders)
               .HasForeignKey(ap => ap.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
