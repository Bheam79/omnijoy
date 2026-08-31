using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        // The directed pair is both the entity identity and the uniqueness
        // constraint, so duplicate follows cannot be persisted.
        builder.HasKey(f => new { f.FollowerId, f.FolloweeId });

        builder.Property(f => f.CreatedAt)
               .IsRequired();

        // CreatedAt plus the opposite user id gives deterministic, index-backed
        // cursor/offset paging for both sides of the relationship.
        builder.HasIndex(f => new { f.FollowerId, f.CreatedAt, f.FolloweeId });
        builder.HasIndex(f => new { f.FolloweeId, f.CreatedAt, f.FollowerId });

        builder.HasOne(f => f.Follower)
               .WithMany(u => u.Following)
               .HasForeignKey(f => f.FollowerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Followee)
               .WithMany(u => u.Followers)
               .HasForeignKey(f => f.FolloweeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
