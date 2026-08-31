using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public sealed class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> builder)
    {
        builder.HasKey(reaction => reaction.Id);

        builder.Property(reaction => reaction.Id)
            .ValueGeneratedOnAdd();

        builder.Property(reaction => reaction.ReactionType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(reaction => reaction.CreatedAt)
            .IsRequired();

        builder.HasIndex(reaction => new { reaction.CommentId, reaction.UserId })
            .IsUnique();

        builder.HasIndex(reaction => reaction.CommentId);
        builder.HasIndex(reaction => reaction.UserId);

        // Comments are soft-deleted, so their historical reactions remain stored.
        builder.HasOne(reaction => reaction.Comment)
            .WithMany(comment => comment.Reactions)
            .HasForeignKey(reaction => reaction.CommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reaction => reaction.User)
            .WithMany(user => user.CommentReactions)
            .HasForeignKey(reaction => reaction.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
