using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class SharedPostConfiguration : IEntityTypeConfiguration<SharedPost>
{
    public void Configure(EntityTypeBuilder<SharedPost> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
               .ValueGeneratedOnAdd();

        builder.Property(s => s.TargetType)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(s => s.OptionalMessage)
               .HasColumnType("text");

        builder.Property(s => s.CreatedAt)
               .IsRequired();

        // Indexes for feed queries
        builder.HasIndex(s => s.SharerId);
        builder.HasIndex(s => new { s.SharerId, s.CreatedAt });
        builder.HasIndex(s => new { s.TargetType, s.TargetId });
        builder.HasIndex(s => s.OriginalPostId);

        builder.HasOne(s => s.OriginalPost)
               .WithMany()
               .HasForeignKey(s => s.OriginalPostId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Sharer)
               .WithMany()
               .HasForeignKey(s => s.SharerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
