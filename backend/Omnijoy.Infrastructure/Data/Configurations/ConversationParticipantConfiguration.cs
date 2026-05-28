using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        // Composite primary key
        builder.HasKey(cp => new { cp.ConversationId, cp.UserId });

        builder.Property(cp => cp.JoinedAt)
               .IsRequired();

        builder.Property(cp => cp.LastReadAt)
               .IsRequired(false);

        builder.HasOne(cp => cp.Conversation)
               .WithMany(c => c.Participants)
               .HasForeignKey(cp => cp.ConversationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cp => cp.User)
               .WithMany(u => u.ConversationParticipants)
               .HasForeignKey(cp => cp.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
