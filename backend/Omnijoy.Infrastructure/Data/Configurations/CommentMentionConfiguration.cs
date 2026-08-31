using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public sealed class CommentMentionConfiguration : IEntityTypeConfiguration<CommentMention>
{
    public void Configure(EntityTypeBuilder<CommentMention> builder)
    {
        builder.HasKey(mention => new { mention.CommentId, mention.MentionedUserId });

        builder.Property(mention => mention.MatchedSlug)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(mention => mention.CreatedAt)
            .IsRequired();

        builder.HasIndex(mention => mention.MentionedUserId);

        builder.HasOne(mention => mention.Comment)
            .WithMany(comment => comment.Mentions)
            .HasForeignKey(mention => mention.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mention => mention.MentionedUser)
            .WithMany(user => user.CommentMentions)
            .HasForeignKey(mention => mention.MentionedUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
