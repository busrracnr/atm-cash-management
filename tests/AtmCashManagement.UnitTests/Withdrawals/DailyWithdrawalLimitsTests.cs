using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Withdrawals;

namespace AtmCashManagement.UnitTests.Withdrawals;

public sealed class DailyWithdrawalLimitsTests
{
    [Theory]
    [InlineData(Currency.TRY, 20_000)]
    [InlineData(Currency.USD, 1_000)]
    [InlineData(Currency.EUR, 1_000)]
    public void Each_currency_has_the_specified_daily_limit(Currency currency, int expected)
    {
        Assert.Equal(expected, DailyWithdrawalLimits.For(currency));
    }
}
