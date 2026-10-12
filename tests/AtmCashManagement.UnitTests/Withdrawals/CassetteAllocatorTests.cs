using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Withdrawals;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Withdrawals;

public sealed class CassetteAllocatorTests
{
    [Fact]
    public void Each_note_comes_from_the_cassette_holding_it()
    {
        var atm = AtmScenarios.Loaded();

        var allocations = CassetteAllocator.Allocate(atm.Cassettes, Currency.TRY, Breakdown((100, 2), (50, 1), (20, 3)));

        Assert.Equal(
            [new CassetteAllocation(2, 100, 2), new CassetteAllocation(3, 50, 1), new CassetteAllocation(4, 20, 3)],
            allocations);
    }

    [Fact]
    public void With_two_cassettes_of_the_same_note_the_fuller_one_pays_first()
    {
        var atm = AtmWithTwoTry100Cassettes(slot2Notes: 30, slot3Notes: 70);

        var allocations = CassetteAllocator.Allocate(atm.Cassettes, Currency.TRY, Breakdown((100, 50)));

        Assert.Equal([new CassetteAllocation(3, 100, 50)], allocations);
    }

    [Fact]
    public void When_the_fuller_cassette_runs_out_the_other_one_continues()
    {
        var atm = AtmWithTwoTry100Cassettes(slot2Notes: 30, slot3Notes: 70);

        var allocations = CassetteAllocator.Allocate(atm.Cassettes, Currency.TRY, Breakdown((100, 80)));

        Assert.Equal([new CassetteAllocation(3, 100, 70), new CassetteAllocation(2, 100, 10)], allocations);
    }

    [Fact]
    public void Equally_full_cassettes_are_used_in_slot_order()
    {
        var atm = AtmWithTwoTry100Cassettes(slot2Notes: 40, slot3Notes: 40);

        var allocations = CassetteAllocator.Allocate(atm.Cassettes, Currency.TRY, Breakdown((100, 45)));

        Assert.Equal([new CassetteAllocation(2, 100, 40), new CassetteAllocation(3, 100, 5)], allocations);
    }

    [Fact]
    public void Available_notes_are_summed_per_denomination_across_cassettes()
    {
        var atm = AtmWithTwoTry100Cassettes(slot2Notes: 30, slot3Notes: 70);

        var available = atm.AvailableNotes(Currency.TRY);

        Assert.Equal(100, available[100]);
        Assert.Equal(25, available[20]);
        Assert.Single(atm.AvailableNotes(Currency.USD));
    }

    private static Atm AtmWithTwoTry100Cassettes(int slot2Notes, int slot3Notes)
    {
        var atm = AtmScenarios.Unconfigured();
        atm.ConfigureCassettes(
        [
            new CassetteDefinition(1, CassetteType.Deposit),
            new CassetteDefinition(2, CassetteType.Recycling, Currency.TRY, 100),
            new CassetteDefinition(3, CassetteType.Recycling, Currency.TRY, 100),
            new CassetteDefinition(4, CassetteType.Recycling, Currency.TRY, 20),
            new CassetteDefinition(5, CassetteType.Recycling, Currency.USD, 100),
        ], AtmScenarios.Now);
        atm.LoadCash([new CashLoadItem(2, slot2Notes), new CashLoadItem(3, slot3Notes), new CashLoadItem(4, 25), new CashLoadItem(5, 10)], AtmScenarios.Context());
        return atm;
    }

    private static NoteBreakdown Breakdown(params (int Denomination, int Count)[] notes) =>
        new(notes.Select(n => new NoteQuantity(n.Denomination, n.Count)));
}
