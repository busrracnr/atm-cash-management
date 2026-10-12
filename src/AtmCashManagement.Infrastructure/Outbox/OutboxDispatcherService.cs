using AtmCashManagement.Application.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtmCashManagement.Infrastructure.Outbox;

/// <summary>Reads pending outbox messages, publishes (logs) them and marks them as sent (requirement B10).</summary>
internal sealed class OutboxDispatcherService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Messages stay unprocessed and are retried on the next tick.
                logger.LogError(ex, "Dispatching outbox messages failed");
            }
        }
    }
}
