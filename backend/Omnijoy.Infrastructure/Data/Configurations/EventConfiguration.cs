using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
               .ValueGeneratedOnAdd();

        builder.Property(e => e.Title)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(e => e.Description)
               .HasColumnType("text");

        builder.Property(e => e.Location)
               .HasMaxLength(512);

        builder.Property(e => e.CoverImageUrl)
               .HasMaxLength(2048);

        builder.Property(e => e.TicketUrl)
               .HasMaxLength(2048);

        builder.Property(e => e.Privacy)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(e => e.PostingPolicy)
               .HasConversion<string>()
               .HasMaxLength(32)
               .HasDefaultValue(Omnijoy.Core.Models.Enums.EventPostingPolicy.OrganizerOnly);

        builder.Property(e => e.CreatedAt)
               .IsRequired();

        builder.HasIndex(e => e.CreatorUserId);
        builder.HasIndex(e => e.CompanyPageId);
        builder.HasIndex(e => e.StartAt);

        builder.HasOne(e => e.CreatorUser)
               .WithMany(u => u.CreatedEvents)
               .HasForeignKey(e => e.CreatorUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CompanyPage)
               .WithMany(cp => cp.Events)
               .HasForeignKey(e => e.CompanyPageId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
