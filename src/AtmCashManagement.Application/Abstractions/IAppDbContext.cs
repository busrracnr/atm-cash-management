using AtmCashManagement.Domain.Atms;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Abstractions;

/// <summary>Persistence port used by the use cases; implemented by the EF Core context in Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Atm> Atms { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
