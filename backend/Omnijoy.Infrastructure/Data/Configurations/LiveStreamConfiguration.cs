using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class LiveStreamConfiguration : IEntityTypeConfiguration<LiveStream>
{
    public void Configure(EntityTypeBuilder<LiveStream> builder)
    {
        builder.HasKey(ls => ls.Id);

        builder.Property(ls => ls.Id)
               .ValueGeneratedOnAdd();

        builder.Property(ls => ls.Title)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(ls => ls.Status)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(ls => ls.Privacy)
               .HasConversion<string>()
               .HasMaxLength(32)
               .HasDefaultValue(Omnijoy.Core.Models.Enums.PrivacyLevel.Friends);

        builder.Property(ls => ls.StreamKey)
               .IsRequired()
               .HasMaxLength(128);

        builder.HasIndex(ls => ls.StreamKey)
               .IsUnique();

        builder.HasIndex(ls => ls.HostUserId);
        builder.HasIndex(ls => new { ls.HostUserId, ls.Status });

        builder.HasOne(ls => ls.HostUser)
               .WithMany(u => u.LiveStreams)
               .HasForeignKey(ls => ls.HostUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
