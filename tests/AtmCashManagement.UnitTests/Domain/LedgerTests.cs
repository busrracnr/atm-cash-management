using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class LedgerTests
{
    [Fact]
    public void Every_balance_equals_the_sum_of_its_movements()
    {
        var atm = AtmScenarios.Configured();
        var ledger = new List<CassetteMovement>();

        atm.LoadCash(AtmScenarios.StandardLoad(), AtmScenarios.Context());
        ledger.AddRange(atm.DequeuePendingMovements());
        atm.ChangeStatus(AtmStatus.InService);
        atm.ChangeStatus(AtmStatus.Maintenance);
        atm.LoadCash([new CashLoadItem(3, 60), new CashLoadItem(4, 5)], AtmScenarios.Context());
        ledger.AddRange(atm.DequeuePendingMovements());
        atm.UnloadCash([4], AtmScenarios.Context());
        ledger.AddRange(atm.DequeuePendingMovements());
        atm.LoadCash([new CashLoadItem(4, 12)], AtmScenarios.Context());
        ledger.AddRange(atm.DequeuePendingMovements());

        foreach (var cassette in atm.Cassettes)
        {
            foreach (var balance in cassette.Balances)
            {
                var entries = ledger.Where(m =>
                    m.CassetteId == cassette.Id && m.Currency == balance.Currency && m.Denomination == balance.Denomination).ToList();

                Assert.Equal(balance.NoteCount, entries.Sum(m => m.NoteCountDelta));
                Assert.Equal(balance.ReservedCount, entries.Sum(m => m.ReservedCountDelta));
            }
        }

        Assert.Equal(12, atm.GetCassette(4).NoteCount);
        Assert.Equal(100, atm.GetCassette(3).NoteCount);
    }

    [Fact]
    public void Pending_movements_are_handed_out_once()
    {
        var atm = AtmScenarios.Configured();
        atm.LoadCash([new CashLoadItem(2, 10)], AtmScenarios.Context());

        Assert.Single(atm.DequeuePendingMovements());
        Assert.Empty(atm.DequeuePendingMovements());
    }

    [Fact]
    public void Movements_reference_the_atm_and_cassette_they_changed()
    {
        var atm = AtmScenarios.Configured();
        atm.LoadCash([new CashLoadItem(3, 10)], AtmScenarios.Context());

        var movement = Assert.Single(atm.DequeuePendingMovements());

        Assert.Equal(atm.Id, movement.AtmId);
        Assert.Equal(atm.GetCassette(3).Id, movement.CassetteId);
        Assert.Equal(3, movement.CassetteSlot);
        Assert.NotEqual(Guid.Empty, movement.Id);
    }
}
