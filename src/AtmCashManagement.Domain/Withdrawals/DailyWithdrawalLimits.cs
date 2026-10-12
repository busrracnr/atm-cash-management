using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Withdrawals;

/// <summary>Per card and business day, tracked separately for each currency (requirement B5).</summary>
public static class DailyWithdrawalLimits
{
    public static int For(Currency currency) => currency switch
    {
        Currency.TRY => 20_000,
        Currency.USD => 1_000,
        Currency.EUR => 1_000,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null),
    };
}
