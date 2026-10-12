using AtmCashManagement.Domain.Cash;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("exchange_rates");

        builder.HasKey(r => r.Currency);
        builder.Property(r => r.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(r => r.RateToTry).HasPrecision(18, 6);

        // Illustrative seed rates; operations keep them current through PUT /api/exchange-rates/{currency}.
        builder.HasData(
            new ExchangeRate(Currency.TRY, 1m, SeededAt),
            new ExchangeRate(Currency.USD, 42.00m, SeededAt),
            new ExchangeRate(Currency.EUR, 49.00m, SeededAt));
    }
}
