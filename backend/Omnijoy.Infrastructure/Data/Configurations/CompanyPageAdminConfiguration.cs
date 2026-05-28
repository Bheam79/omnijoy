using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class CompanyPageAdminConfiguration : IEntityTypeConfiguration<CompanyPageAdmin>
{
    public void Configure(EntityTypeBuilder<CompanyPageAdmin> builder)
    {
        // Composite primary key
        builder.HasKey(cpa => new { cpa.CompanyPageId, cpa.UserId });

        builder.Property(cpa => cpa.Role)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.HasOne(cpa => cpa.CompanyPage)
               .WithMany(cp => cp.Admins)
               .HasForeignKey(cpa => cpa.CompanyPageId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cpa => cpa.User)
               .WithMany(u => u.CompanyPageAdminships)
               .HasForeignKey(cpa => cpa.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
