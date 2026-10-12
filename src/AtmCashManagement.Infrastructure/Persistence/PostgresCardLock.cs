using AtmCashManagement.Application.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Infrastructure.Persistence;

/// <summary>Transaction-scoped PostgreSQL advisory lock keyed by the card hash; released on commit or rollback.</summary>
internal sealed class PostgresCardLock(AppDbContext db) : ICardLock
{
    public async Task AcquireAsync(string cardHash, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The card lock must be taken inside a transaction, otherwise it is released at once.");
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({cardHash}, 0))", cancellationToken);
    }
}
