using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.ExchangeRates;

public sealed record ExchangeRateResponse(Currency Currency, decimal RateToTry, DateTimeOffset UpdatedAt);

/// <param name="RateToTry">TRY value of one unit of the currency.</param>
public sealed record UpdateExchangeRateRequest([Range(typeof(decimal), "0.000001", "1000000")] decimal RateToTry);

public sealed class ExchangeRateService(IAppDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<ExchangeRateResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.ExchangeRates
            .AsNoTracking()
            .OrderBy(r => r.Currency)
            .Select(r => new ExchangeRateResponse(r.Currency, r.RateToTry, r.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<ExchangeRateResponse> UpdateAsync(Currency currency, UpdateExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var rate = await db.ExchangeRates.FirstOrDefaultAsync(r => r.Currency == currency, cancellationToken)
            ?? throw new DomainException(new Error("EXCHANGE_RATE_NOT_FOUND", $"No exchange rate for {currency}.", ErrorType.NotFound));

        rate.Update(request.RateToTry, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return new ExchangeRateResponse(rate.Currency, rate.RateToTry, rate.UpdatedAt);
    }
}
