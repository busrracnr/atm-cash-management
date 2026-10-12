using AtmCashManagement.Application.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Type).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);

        // The dispatcher only ever scans unprocessed messages in order.
        builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL");
        builder.HasIndex(m => new { m.AtmId, m.OccurredAt });
    }
}
