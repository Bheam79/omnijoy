using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class SavedPostCollectionConfiguration : IEntityTypeConfiguration<SavedPostCollection>
{
    public void Configure(EntityTypeBuilder<SavedPostCollection> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
               .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(c => c.CreatedAt)
               .IsRequired();

        builder.HasIndex(c => c.UserId);

        builder.HasOne(c => c.User)
               .WithMany(u => u.SavedPostCollections)
               .HasForeignKey(c => c.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
