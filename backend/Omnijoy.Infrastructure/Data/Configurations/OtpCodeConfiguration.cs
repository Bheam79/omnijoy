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

        builder.HasIndex(o => o.Email);
        builder.HasIndex(o => new { o.Email, o.IsUsed, o.ExpiresAt });
    }
}
