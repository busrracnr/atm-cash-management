namespace AtmCashManagement.Application.Abstractions;

public interface IOutboxProcessor
{
    /// <summary>Publishes a batch of unprocessed outbox messages and marks them sent; returns how many were sent.</summary>
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken);
}
