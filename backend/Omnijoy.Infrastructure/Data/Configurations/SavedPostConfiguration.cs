using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class SavedPostConfiguration : IEntityTypeConfiguration<SavedPost>
{
    public void Configure(EntityTypeBuilder<SavedPost> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
               .ValueGeneratedOnAdd();

        builder.Property(s => s.CreatedAt)
               .IsRequired();

        // A post can appear in at most one collection for a given user.
        builder.HasIndex(s => new { s.UserId, s.PostId })
               .IsUnique();

        builder.HasIndex(s => new { s.UserId, s.CreatedAt, s.Id });
        builder.HasIndex(s => s.PostId);
        builder.HasIndex(s => s.CollectionId);

        // These match PostReaction: deleting either owner or post removes the
        // dependent bookmark, while Post.Author itself remains Restrict.
        builder.HasOne(s => s.User)
               .WithMany(u => u.SavedPosts)
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Post)
               .WithMany(p => p.SavedBy)
               .HasForeignKey(s => s.PostId)
               .OnDelete(DeleteBehavior.Cascade);

        // Collection deletion returns bookmarks to Uncategorized.
        builder.HasOne(s => s.Collection)
               .WithMany(c => c.SavedPosts)
               .HasForeignKey(s => s.CollectionId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
