using AtmCashManagement.Application.Idempotency;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        // The primary key is the last line of defence against two concurrent requests with the same key.
        builder.HasKey(r => new { r.Scope, r.Key });

        builder.Property(r => r.Scope).HasMaxLength(50);
        builder.Property(r => r.Key).HasMaxLength(IdempotencyRecord.KeyMaxLength);
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ResponseBody).HasColumnType("jsonb").IsRequired();
    }
}
