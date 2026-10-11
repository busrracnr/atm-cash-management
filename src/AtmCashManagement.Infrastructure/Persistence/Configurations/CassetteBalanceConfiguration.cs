using AtmCashManagement.Domain.Atms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class CassetteBalanceConfiguration : IEntityTypeConfiguration<CassetteBalance>
{
    public void Configure(EntityTypeBuilder<CassetteBalance> builder)
    {
        builder.ToTable("cassette_balances", table =>
        {
            table.HasCheckConstraint("ck_cassette_balances_note_count", $"note_count BETWEEN 0 AND {Cassette.Capacity}");
            table.HasCheckConstraint("ck_cassette_balances_reserved_count", "reserved_count BETWEEN 0 AND note_count");
        });

        builder.HasKey(b => new { b.CassetteId, b.Currency, b.Denomination });

        builder.Property(b => b.Currency).HasConversion<string>().HasMaxLength(3);
    }
}
