using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class EventAttendeeConfiguration : IEntityTypeConfiguration<EventAttendee>
{
    public void Configure(EntityTypeBuilder<EventAttendee> builder)
    {
        // Composite primary key
        builder.HasKey(ea => new { ea.EventId, ea.UserId });

        builder.Property(ea => ea.RSVP)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.HasOne(ea => ea.Event)
               .WithMany(e => e.Attendees)
               .HasForeignKey(ea => ea.EventId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ea => ea.User)
               .WithMany(u => u.EventAttendances)
               .HasForeignKey(ea => ea.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
