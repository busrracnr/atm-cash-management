using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Infrastructure.Persistence;

internal sealed class AtmRepository(AppDbContext db) : IAtmRepository
{
    public async Task<Atm> GetForUpdateAsync(Guid atmId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("GetForUpdateAsync must run inside a transaction, otherwise the lock is released at once.");
        }

        // Concurrent operations on the same ATM block here until this transaction commits or rolls back.
        var locked = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM atms WHERE id = {atmId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        if (locked.Count == 0)
        {
            throw new DomainException(AtmErrors.NotFound(atmId));
        }

        return await db.Atms
            .Include(a => a.Cassettes).ThenInclude(c => c.Balances)
            .SingleAsync(a => a.Id == atmId, cancellationToken);
    }
}
