using AtmCashManagement.Application.Idempotency;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Reconciliations;
using AtmCashManagement.Domain.Withdrawals;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Abstractions;

/// <summary>Persistence port used by the use cases; implemented by the EF Core context in Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Atm> Atms { get; }

    DbSet<Cassette> Cassettes { get; }

    DbSet<CassetteBalance> CassetteBalances { get; }

    DbSet<CassetteMovement> CassetteMovements { get; }

    DbSet<WithdrawalQuote> WithdrawalQuotes { get; }

    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    DbSet<Reconciliation> Reconciliations { get; }

    DbSet<ExchangeRate> ExchangeRates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
