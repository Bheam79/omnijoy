using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class FamilyRelationConfiguration : IEntityTypeConfiguration<FamilyRelation>
{
    public void Configure(EntityTypeBuilder<FamilyRelation> builder)
    {
        builder.HasKey(fr => fr.Id);

        builder.Property(fr => fr.Id)
               .ValueGeneratedOnAdd();

        builder.Property(fr => fr.RelationType)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(fr => fr.Subtype)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(fr => fr.CreatedAt)
               .IsRequired();

        builder.HasIndex(fr => fr.UserId);
        builder.HasIndex(fr => fr.RelatedUserId);
        builder.HasIndex(fr => new { fr.UserId, fr.RelatedUserId });

        builder.HasOne(fr => fr.User)
               .WithMany(u => u.FamilyRelations)
               .HasForeignKey(fr => fr.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(fr => fr.RelatedUser)
               .WithMany()
               .HasForeignKey(fr => fr.RelatedUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
