using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.Id)
               .ValueGeneratedOnAdd();

        builder.Property(pm => pm.Url)
               .IsRequired()
               .HasMaxLength(2048);

        builder.Property(pm => pm.ThumbnailUrl)
               .HasMaxLength(2048);

        builder.Property(pm => pm.MediaType)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.HasIndex(pm => pm.PostId);

        builder.HasOne(pm => pm.Post)
               .WithMany(p => p.Media)
               .HasForeignKey(pm => pm.PostId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
