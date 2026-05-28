using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class FriendConfiguration : IEntityTypeConfiguration<Friend>
{
    public void Configure(EntityTypeBuilder<Friend> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
               .ValueGeneratedOnAdd();

        builder.Property(f => f.Status)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(f => f.CreatedAt)
               .IsRequired();

        builder.Property(f => f.UpdatedAt)
               .IsRequired();

        // Composite unique index: only one friendship between two users
        builder.HasIndex(f => new { f.RequesterId, f.AddresseeId })
               .IsUnique();

        builder.HasIndex(f => f.AddresseeId);
        builder.HasIndex(f => new { f.AddresseeId, f.Status });

        builder.HasOne(f => f.Requester)
               .WithMany(u => u.SentFriendRequests)
               .HasForeignKey(f => f.RequesterId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Addressee)
               .WithMany(u => u.ReceivedFriendRequests)
               .HasForeignKey(f => f.AddresseeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
