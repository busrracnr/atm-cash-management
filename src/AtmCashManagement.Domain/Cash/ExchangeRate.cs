using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Cash;

/// <summary>TRY value of one unit of a currency, used to value reconciliation differences and report totals.</summary>
public sealed class ExchangeRate
{
    // Required by EF Core for materialisation.
    private ExchangeRate()
    {
    }

    public ExchangeRate(Currency currency, decimal rateToTry, DateTimeOffset updatedAt)
    {
        Currency = currency;
        Update(rateToTry, updatedAt);
    }

    public Currency Currency { get; private set; }

    public decimal RateToTry { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(decimal rateToTry, DateTimeOffset updatedAt)
    {
        if (rateToTry <= 0 || (Currency == Currency.TRY && rateToTry != 1))
        {
            throw new DomainException(new Error(
                "EXCHANGE_RATE_INVALID",
                Currency == Currency.TRY ? "The TRY rate is always 1." : "An exchange rate must be positive.",
                ErrorType.Validation));
        }

        RateToTry = rateToTry;
        UpdatedAt = updatedAt;
    }
}
