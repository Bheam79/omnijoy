using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omnijoy.Core.Models;

namespace Omnijoy.Infrastructure.Data.Configurations;

public class MessageMediaConfiguration : IEntityTypeConfiguration<MessageMedia>
{
    public void Configure(EntityTypeBuilder<MessageMedia> builder)
    {
        builder.HasKey(mm => mm.Id);

        builder.Property(mm => mm.Id)
               .ValueGeneratedOnAdd();

        builder.Property(mm => mm.Url)
               .IsRequired()
               .HasMaxLength(2048);

        builder.Property(mm => mm.FileName)
               .HasMaxLength(512);

        builder.Property(mm => mm.MediaType)
               .HasConversion<string>()
               .HasMaxLength(32);

        builder.HasIndex(mm => mm.MessageId);

        builder.HasOne(mm => mm.Message)
               .WithMany(m => m.Media)
               .HasForeignKey(mm => mm.MessageId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
