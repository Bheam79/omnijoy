using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class FriendInviteConfiguration : IEntityTypeConfiguration<FriendInvite>
{
    public void Configure(EntityTypeBuilder<FriendInvite> builder)
    {
        builder.HasKey(fi => fi.Id);

        builder.Property(fi => fi.Id)
               .ValueGeneratedOnAdd();

        builder.Property(fi => fi.Token)
               .IsRequired()
               .HasMaxLength(64);

        builder.HasIndex(fi => fi.Token)
               .IsUnique();

        builder.Property(fi => fi.Email)
               .HasMaxLength(256);

        builder.HasIndex(fi => fi.Email);

        builder.Property(fi => fi.Status)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(fi => fi.ExpiresAt)
               .IsRequired();

        builder.Property(fi => fi.CreatedAt)
               .IsRequired();

        builder.HasOne(fi => fi.Inviter)
               .WithMany()
               .HasForeignKey(fi => fi.InviterId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fi => fi.AcceptedBy)
               .WithMany()
               .HasForeignKey(fi => fi.AcceptedByUserId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);
    }
}
