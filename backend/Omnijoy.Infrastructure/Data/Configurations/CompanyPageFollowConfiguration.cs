using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class CompanyPageFollowConfiguration : IEntityTypeConfiguration<CompanyPageFollow>
{
    public void Configure(EntityTypeBuilder<CompanyPageFollow> builder)
    {
        // Composite primary key
        builder.HasKey(f => new { f.CompanyPageId, f.UserId });

        builder.Property(f => f.CreatedAt)
               .IsRequired();

        builder.HasOne(f => f.CompanyPage)
               .WithMany(cp => cp.Followers)
               .HasForeignKey(f => f.CompanyPageId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.User)
               .WithMany(u => u.CompanyPageFollows)
               .HasForeignKey(f => f.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.UserId);
    }
}
