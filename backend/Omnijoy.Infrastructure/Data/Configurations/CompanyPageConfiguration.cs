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

        builder.HasOne(cp => cp.CreatedByUser)
               .WithMany(u => u.CreatedCompanyPages)
               .HasForeignKey(cp => cp.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
