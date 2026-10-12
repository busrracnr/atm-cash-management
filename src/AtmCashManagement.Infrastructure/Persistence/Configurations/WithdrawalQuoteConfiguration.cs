using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Withdrawals;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class WithdrawalQuoteConfiguration : IEntityTypeConfiguration<WithdrawalQuote>
{
    public void Configure(EntityTypeBuilder<WithdrawalQuote> builder)
    {
        builder.ToTable("withdrawal_quotes", table =>
            table.HasCheckConstraint("ck_withdrawal_quotes_amounts", "requested_amount > 0 AND limit_amount >= 0"));

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.CardHash).HasMaxLength(WithdrawalQuote.CardHashLength).IsFixedLength().IsRequired();
        builder.Property(q => q.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(q => q.ConfirmedOption).HasConversion<string>().HasMaxLength(8);

        builder.HasOne<Atm>().WithMany().HasForeignKey(q => q.AtmId).OnDelete(DeleteBehavior.Restrict);

        // Daily-limit lookups, and the sweep that expires open quotes.
        builder.HasIndex(q => new { q.CardHash, q.Currency, q.BusinessDate });
        builder.HasIndex(q => new { q.AtmId, q.ExpiresAt }).HasFilter("status = 'Open'");

        builder.OwnsMany(q => q.Lines, lines =>
        {
            lines.ToTable("withdrawal_quote_lines");
            lines.WithOwner().HasForeignKey("QuoteId");
            lines.Property(l => l.Option).HasConversion<string>().HasMaxLength(8);
            lines.HasKey("QuoteId", nameof(QuoteLine.Option), nameof(QuoteLine.Slot));
        });
        builder.Navigation(q => q.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
