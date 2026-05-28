using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
               .ValueGeneratedOnAdd();

        builder.Property(r => r.ReactionType)
               .HasConversion<string>()
               .HasMaxLength(16)
               .IsRequired();

        builder.Property(r => r.CreatedAt)
               .IsRequired();

        // Unique constraint: one reaction per user per post
        builder.HasIndex(r => new { r.PostId, r.UserId })
               .IsUnique();

        builder.HasIndex(r => r.PostId);
        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.Post)
               .WithMany(p => p.Reactions)
               .HasForeignKey(r => r.PostId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
