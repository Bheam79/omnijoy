using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class ModerationLogConfiguration : IEntityTypeConfiguration<ModerationLog>
{
    public void Configure(EntityTypeBuilder<ModerationLog> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.Action)
               .HasConversion<string>()
               .HasMaxLength(32)
               .IsRequired();

        builder.Property(m => m.TargetType)
               .IsRequired()
               .HasMaxLength(64);

        builder.Property(m => m.TargetId)
               .IsRequired()
               .HasMaxLength(64);

        builder.Property(m => m.Notes)
               .HasMaxLength(2000);

        builder.Property(m => m.CreatedAt)
               .IsRequired();

        // Indexes for the audit-log query endpoint
        builder.HasIndex(m => m.ActorId);
        builder.HasIndex(m => m.Action);
        builder.HasIndex(m => m.CreatedAt);

        builder.HasOne(m => m.Actor)
               .WithMany()
               .HasForeignKey(m => m.ActorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
