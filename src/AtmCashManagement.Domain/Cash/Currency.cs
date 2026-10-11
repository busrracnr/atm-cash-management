namespace AtmCashManagement.Domain.Cash;

public enum Currency
{
    TRY,
    USD,
    EUR,
}

public static class CurrencyExtensions
{
    public static bool IsForeign(this Currency currency) => currency != Currency.TRY;
}
