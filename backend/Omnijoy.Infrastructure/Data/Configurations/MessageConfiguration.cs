using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.Content)
               .HasColumnType("text");

        builder.Property(m => m.MessageType)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.Property(m => m.CreatedAt)
               .IsRequired();

        // Indexes for loading conversations
        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        builder.HasIndex(m => m.SenderUserId);
        builder.HasIndex(m => m.DeletedAt);

        builder.HasOne(m => m.Conversation)
               .WithMany(c => c.Messages)
               .HasForeignKey(m => m.ConversationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
               .WithMany(u => u.SentMessages)
               .HasForeignKey(m => m.SenderUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
