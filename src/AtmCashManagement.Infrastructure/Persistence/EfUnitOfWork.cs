using AtmCashManagement.Application.Abstractions;

namespace AtmCashManagement.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        // Disposing without commit rolls back, so a failed operation leaves no partial writes or locks.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var result = await operation(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
