using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
               .ValueGeneratedOnAdd();

        builder.Property(p => p.Content)
               .HasColumnType("text");

        builder.Property(p => p.BackgroundImageUrl)
               .HasMaxLength(2048);

        // ── Embedded URL / OG preview ────────────────────────────────────────
        builder.Property(p => p.LinkUrl)
               .HasMaxLength(2048);

        builder.Property(p => p.LinkTitle)
               .HasMaxLength(512);

        builder.Property(p => p.LinkDescription)
               .HasColumnType("text");

        builder.Property(p => p.LinkImageUrl)
               .HasMaxLength(2048);

        builder.Property(p => p.LinkSiteName)
               .HasMaxLength(256);

        builder.Property(p => p.PostType)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(p => p.Privacy)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(p => p.CreatedAt)
               .IsRequired();

        builder.Property(p => p.UpdatedAt)
               .IsRequired();

        // Indexes for common queries
        builder.HasIndex(p => p.AuthorUserId);
        builder.HasIndex(p => p.CompanyPageId);
        builder.HasIndex(p => new { p.AuthorUserId, p.CreatedAt });
        builder.HasIndex(p => p.DeletedAt);

        builder.HasOne(p => p.Author)
               .WithMany(u => u.Posts)
               .HasForeignKey(p => p.AuthorUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CompanyPage)
               .WithMany(cp => cp.Posts)
               .HasForeignKey(p => p.CompanyPageId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
