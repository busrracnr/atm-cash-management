using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Ledger;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class CassetteMovementConfiguration : IEntityTypeConfiguration<CassetteMovement>
{
    public void Configure(EntityTypeBuilder<CassetteMovement> builder)
    {
        builder.ToTable("cassette_movements");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Sequence).UseIdentityAlwaysColumn();
        builder.HasIndex(m => m.Sequence).IsUnique();

        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(m => m.PerformedBy).HasMaxLength(CurrentUserLimits.NameMaxLength).IsRequired();

        builder.HasIndex(m => new { m.AtmId, m.Sequence });
        builder.HasIndex(m => m.CassetteId);

        builder.HasOne<Atm>().WithMany().HasForeignKey(m => m.AtmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Cassette>().WithMany().HasForeignKey(m => m.CassetteId).OnDelete(DeleteBehavior.Restrict);
    }
}
