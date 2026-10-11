using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class CashLoadingTests
{
    [Fact]
    public void Loading_records_notes_and_a_load_movement_per_cassette()
    {
        var atm = AtmScenarios.Configured();

        atm.LoadCash([new CashLoadItem(2, 50), new CashLoadItem(5, 20)], AtmScenarios.Context("ops.alice"));

        Assert.Equal(AtmStatus.Loaded, atm.Status);
        Assert.Equal(50, atm.GetCassette(2).NoteCount);
        Assert.Equal(20, atm.GetCassette(5).NoteCount);

        var movements = atm.DequeuePendingMovements();
        Assert.Equal(2, movements.Count);
        var usd = Assert.Single(movements, m => m.CassetteSlot == 5);
        Assert.Equal(MovementType.Load, usd.Type);
        Assert.Equal((Currency.USD, 100, 20, 0), (usd.Currency, usd.Denomination, usd.NoteCountDelta, usd.ReservedCountDelta));
        Assert.Equal("ops.alice", usd.PerformedBy);
        Assert.Equal(AtmScenarios.Now, usd.OccurredAt);
    }

    [Fact]
    public void Loading_in_maintenance_adds_to_existing_notes_and_keeps_the_status()
    {
        var atm = AtmScenarios.InMaintenance();

        atm.LoadCash([new CashLoadItem(2, 25)], AtmScenarios.Context());

        Assert.Equal(AtmStatus.Maintenance, atm.Status);
        Assert.Equal(75, atm.GetCassette(2).NoteCount);
    }

    [Fact]
    public void A_cassette_can_be_filled_up_to_exactly_its_capacity()
    {
        var atm = AtmScenarios.Configured();

        atm.LoadCash([new CashLoadItem(2, Cassette.Capacity)], AtmScenarios.Context());

        Assert.Equal(0, atm.GetCassette(2).FreeCapacity);
    }

    [Fact]
    public void Loading_beyond_capacity_is_rejected()
    {
        var atm = AtmScenarios.InMaintenance();

        AssertRejected(atm, [new CashLoadItem(2, 51)], "CASSETTE_CAPACITY_EXCEEDED");
    }

    [Fact]
    public void The_deposit_cassette_cannot_be_loaded()
    {
        AssertRejected(AtmScenarios.Configured(), [new CashLoadItem(1, 10)], "CASSETTE_NOT_LOADABLE");
    }

    [Fact]
    public void A_slot_can_appear_only_once_per_load()
    {
        AssertRejected(AtmScenarios.Configured(), [new CashLoadItem(2, 10), new CashLoadItem(2, 5)], "CASH_LOAD_INVALID");
    }

    [Fact]
    public void An_empty_load_is_rejected()
    {
        AssertRejected(AtmScenarios.Configured(), [], "CASH_LOAD_INVALID");
    }

    [Fact]
    public void A_partly_invalid_load_changes_nothing()
    {
        var atm = AtmScenarios.Configured();

        AssertRejected(atm, [new CashLoadItem(2, 10), new CashLoadItem(3, 101)], "CASSETTE_CAPACITY_EXCEEDED");

        Assert.Equal(0, atm.GetCassette(2).NoteCount);
        Assert.Equal(AtmStatus.Configured, atm.Status);
        Assert.Empty(atm.DequeuePendingMovements());
    }

    [Fact]
    public void Unloading_empties_the_cassettes_with_negative_movements()
    {
        var atm = AtmScenarios.InMaintenance();
        atm.DequeuePendingMovements();

        atm.UnloadCash([2, 5], AtmScenarios.Context());

        Assert.True(atm.GetCassette(2).IsEmpty);
        Assert.True(atm.GetCassette(5).IsEmpty);
        Assert.Equal(40, atm.GetCassette(3).NoteCount);
        var movements = atm.DequeuePendingMovements();
        Assert.All(movements, m => Assert.Equal(MovementType.Unload, m.Type));
        Assert.Equal([-50, -20], movements.OrderBy(m => m.CassetteSlot).Select(m => m.NoteCountDelta));
    }

    [Fact]
    public void Unloading_an_unknown_slot_is_rejected()
    {
        var atm = AtmScenarios.InMaintenance();

        var exception = Assert.Throws<DomainException>(() => atm.UnloadCash([7], AtmScenarios.Context()));

        Assert.Equal("CASSETTE_NOT_FOUND", exception.Error.Code);
    }

    private static void AssertRejected(Atm atm, List<CashLoadItem> items, string expectedCode)
    {
        var exception = Assert.Throws<DomainException>(() => atm.LoadCash(items, AtmScenarios.Context()));

        Assert.Equal(expectedCode, exception.Error.Code);
    }
}
