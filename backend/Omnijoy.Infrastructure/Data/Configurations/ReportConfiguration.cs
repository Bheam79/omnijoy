using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
               .ValueGeneratedOnAdd();

        builder.Property(r => r.TargetType)
               .HasConversion<string>()
               .HasMaxLength(32)
               .IsRequired();

        builder.Property(r => r.Reason)
               .HasConversion<string>()
               .HasMaxLength(32)
               .IsRequired();

        builder.Property(r => r.Status)
               .HasConversion<string>()
               .HasMaxLength(32)
               .IsRequired();

        builder.Property(r => r.Notes)
               .HasMaxLength(2000);

        builder.Property(r => r.CreatedAt)
               .IsRequired();

        // Unique index: one report per (reporter, targetType, targetId)
        builder.HasIndex(r => new { r.ReporterId, r.TargetType, r.TargetId })
               .IsUnique();

        // Indexes for admin list queries
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.TargetType);
        builder.HasIndex(r => r.CreatedAt);

        builder.HasOne(r => r.Reporter)
               .WithMany(u => u.FiledReports)
               .HasForeignKey(r => r.ReporterId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
