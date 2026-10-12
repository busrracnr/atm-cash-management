using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Reconciliations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class ReconciliationConfiguration : IEntityTypeConfiguration<Reconciliation>
{
    public void Configure(EntityTypeBuilder<Reconciliation> builder)
    {
        builder.ToTable("reconciliations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.TotalDifferenceTry).HasPrecision(18, 2);
        builder.Property(r => r.CountedBy).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ApprovedBy).HasMaxLength(100);

        builder.HasOne<Atm>().WithMany().HasForeignKey(r => r.AtmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.AtmId, r.CountedAt });

        builder.OwnsMany(r => r.Lines, lines =>
        {
            lines.ToTable("reconciliation_lines");
            lines.WithOwner().HasForeignKey("ReconciliationId");
            lines.Property(l => l.Currency).HasConversion<string>().HasMaxLength(3);
            lines.Property(l => l.DifferenceTry).HasPrecision(18, 2);
            lines.HasKey("ReconciliationId", nameof(ReconciliationLine.Slot), nameof(ReconciliationLine.Currency), nameof(ReconciliationLine.Denomination));
        });
        builder.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
