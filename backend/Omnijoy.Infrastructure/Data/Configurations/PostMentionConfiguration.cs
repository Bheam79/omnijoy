using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public sealed class PostMentionConfiguration : IEntityTypeConfiguration<PostMention>
{
    public void Configure(EntityTypeBuilder<PostMention> builder)
    {
        builder.HasKey(mention => new { mention.PostId, mention.MentionedUserId });

        builder.Property(mention => mention.MatchedSlug)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(mention => mention.CreatedAt)
            .IsRequired();

        builder.HasIndex(mention => mention.MentionedUserId);

        builder.HasOne(mention => mention.Post)
            .WithMany(post => post.Mentions)
            .HasForeignKey(mention => mention.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mention => mention.MentionedUser)
            .WithMany(user => user.PostMentions)
            .HasForeignKey(mention => mention.MentionedUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
