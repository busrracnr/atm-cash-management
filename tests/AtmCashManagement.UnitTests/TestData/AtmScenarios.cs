using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Ledger;

namespace AtmCashManagement.UnitTests.TestData;

/// <summary>Builds ATMs in a given lifecycle state through the public domain API, never by poking at fields.</summary>
internal static class AtmScenarios
{
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    public static MovementContext Context(string user = "ops.alice") => new(user, Now);

    /// <summary>Slot 1 deposit; TRY 100, TRY 50, TRY 20 and USD 100 recycling cassettes.</summary>
    public static List<CassetteDefinition> StandardCassettes() =>
    [
        new(1, CassetteType.Deposit),
        new(2, CassetteType.Recycling, Currency.TRY, 100),
        new(3, CassetteType.Recycling, Currency.TRY, 50),
        new(4, CassetteType.Recycling, Currency.TRY, 20),
        new(5, CassetteType.Recycling, Currency.USD, 100),
    ];

    public static List<CashLoadItem> StandardLoad() => [new(2, 50), new(3, 40), new(4, 30), new(5, 20)];

    public static Atm InState(AtmStatus status) => status switch
    {
        AtmStatus.Unconfigured => Unconfigured(),
        AtmStatus.Configured => Configured(),
        AtmStatus.Loaded => Loaded(),
        AtmStatus.InService => InService(),
        AtmStatus.Maintenance => InMaintenance(),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static Atm Unconfigured() => Atm.Create("IST-KDK-001", "Kadıköy, İstanbul", Now);

    public static Atm Configured()
    {
        var atm = Unconfigured();
        atm.ConfigureCassettes(StandardCassettes(), Now);
        return atm;
    }

    public static Atm Loaded()
    {
        var atm = Configured();
        atm.LoadCash(StandardLoad(), Context());
        return atm;
    }

    public static Atm InService()
    {
        var atm = Loaded();
        atm.ChangeStatus(AtmStatus.InService);
        return atm;
    }

    public static Atm InMaintenance()
    {
        var atm = InService();
        atm.ChangeStatus(AtmStatus.Maintenance);
        return atm;
    }
}
