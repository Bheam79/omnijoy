using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
               .ValueGeneratedOnAdd();

        builder.Property(o => o.Email)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(o => o.CodeHash)
               .IsRequired()
               .HasMaxLength(512);

        builder.Property(o => o.ExpiresAt)
               .IsRequired();

        builder.Property(o => o.CreatedAt)
               .IsRequired();

        // Persist the purpose discriminator as an int column (0 = Login, 1 = PasswordReset).
        // Default value 0 ensures existing rows back-fill to Login without a data migration.
        builder.Property(o => o.Purpose)
               .HasConversion<int>()
               .HasDefaultValue(Omnijoy.Core.Models.Enums.OtpPurpose.Login)
               .IsRequired();

        // AttemptCount tracks wrong-code submissions; default 0.
        builder.Property(o => o.AttemptCount)
               .HasDefaultValue(0)
               .IsRequired();

        builder.HasIndex(o => o.Email);

        // Replace the old (Email, IsUsed, ExpiresAt) index with a Purpose-aware
        // composite so Login and PasswordReset lookups only scan their own partition.
        builder.HasIndex(o => new { o.Email, o.Purpose, o.IsUsed, o.ExpiresAt });
    }
}
