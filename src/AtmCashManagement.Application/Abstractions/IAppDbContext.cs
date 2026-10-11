using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Ledger;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Abstractions;

/// <summary>Persistence port used by the use cases; implemented by the EF Core context in Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Atm> Atms { get; }

    DbSet<Cassette> Cassettes { get; }

    DbSet<CassetteBalance> CassetteBalances { get; }

    DbSet<CassetteMovement> CassetteMovements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
