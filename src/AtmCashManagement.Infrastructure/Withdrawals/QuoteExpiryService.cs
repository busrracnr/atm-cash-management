using AtmCashManagement.Application.Withdrawals;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtmCashManagement.Infrastructure.Withdrawals;

/// <summary>Releases the reservations of quotes whose 60 seconds are over, without waiting for the next request.</summary>
internal sealed class QuoteExpiryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<QuoteExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<WithdrawalService>().ExpireDueQuotesAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Expired {Count} withdrawal quotes and released their notes", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed sweep must not stop the service; the next tick retries.
                logger.LogError(ex, "Expiring withdrawal quotes failed");
            }
        }
    }
}
