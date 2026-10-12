using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AtmCashManagement.Infrastructure.Outbox;

internal sealed class OutboxProcessor(AppDbContext db, TimeProvider clock, ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    private const int BatchSize = 50;

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED lets several API instances share the outbox without publishing a message twice.
        var batch = await db.OutboxMessages
            .FromSql($"SELECT * FROM outbox_messages WHERE processed_at IS NULL ORDER BY occurred_at LIMIT {BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                // Publishing means logging in this assignment; a broker or notification service would be called here.
                logger.LogWarning(
                    "Published {MessageType} {MessageId} for ATM {AtmId}: {Payload}", message.Type, message.Id, message.AtmId, message.Payload);
                message.MarkProcessed(clock.GetUtcNow());
                published++;
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);
                logger.LogError(ex, "Publishing outbox message {MessageId} failed", message.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return published;
    }
}
