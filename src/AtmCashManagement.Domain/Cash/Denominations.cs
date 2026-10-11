namespace AtmCashManagement.Domain.Cash;

/// <summary>Banknote values accepted for every currency. Coins are not supported.</summary>
public static class Denominations
{
    public static IReadOnlyList<int> Supported { get; } = [500, 100, 50, 20];

    public static bool IsSupported(int denomination) => Supported.Contains(denomination);
}
