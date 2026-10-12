using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Deposits;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class DepositTests
{
    private static readonly Guid DepositId = Guid.CreateVersion7();

    [Fact]
    public void Matching_notes_go_to_their_recycling_cassette_and_the_rest_to_the_deposit_cassette()
    {
        var atm = AtmScenarios.InService();

        var result = Deposit(atm, new DepositNote(Currency.TRY, 100, 5), new DepositNote(Currency.TRY, 500, 2), new DepositNote(Currency.EUR, 50, 3));

        Assert.Equal(
            [
                new DepositPlacement(Currency.TRY, 500, 2, 1, CassetteType.Deposit),
                new DepositPlacement(Currency.TRY, 100, 5, 2, CassetteType.Recycling),
                new DepositPlacement(Currency.EUR, 50, 3, 1, CassetteType.Deposit),
            ],
            result.Placements);
        Assert.Empty(result.Returned);
        Assert.Equal(55, atm.GetCassette(2).NoteCount);
        Assert.Equal(5, atm.GetCassette(1).NoteCount);
    }

    [Fact]
    public void A_full_recycling_cassette_overflows_into_the_deposit_cassette()
    {
        var atm = AtmScenarios.InService();

        var result = Deposit(atm, new DepositNote(Currency.TRY, 100, 60));

        Assert.Equal(
            [
                new DepositPlacement(Currency.TRY, 100, 50, 2, CassetteType.Recycling),
                new DepositPlacement(Currency.TRY, 100, 10, 1, CassetteType.Deposit),
            ],
            result.Placements);
        Assert.Equal(Cassette.Capacity, atm.GetCassette(2).NoteCount);
    }

    [Fact]
    public void Notes_that_fit_nowhere_are_returned_to_the_customer()
    {
        var atm = AtmScenarios.InService();

        var result = Deposit(atm, new DepositNote(Currency.TRY, 500, 103));

        Assert.Equal([new DepositPlacement(Currency.TRY, 500, 100, 1, CassetteType.Deposit)], result.Placements);
        Assert.Equal([new DepositReturn(Currency.TRY, 500, 3)], result.Returned);
    }

    [Fact]
    public void When_space_runs_out_larger_notes_are_kept_and_smaller_ones_returned()
    {
        var atm = AtmScenarios.InService();
        Deposit(atm, new DepositNote(Currency.EUR, 500, 95));

        var result = Deposit(atm, new DepositNote(Currency.EUR, 20, 10), new DepositNote(Currency.EUR, 100, 4));

        Assert.Equal(
            [
                new DepositPlacement(Currency.EUR, 100, 4, 1, CassetteType.Deposit),
                new DepositPlacement(Currency.EUR, 20, 1, 1, CassetteType.Deposit),
            ],
            result.Placements);
        Assert.Equal([new DepositReturn(Currency.EUR, 20, 9)], result.Returned);
    }

    [Fact]
    public void With_two_matching_cassettes_the_emptier_one_fills_first()
    {
        var atm = AtmScenarios.WithTwoTry100Cassettes(slot2Notes: 30, slot3Notes: 70);

        var result = Deposit(atm, new DepositNote(Currency.TRY, 100, 10));

        Assert.Equal([new DepositPlacement(Currency.TRY, 100, 10, 2, CassetteType.Recycling)], result.Placements);
    }

    [Fact]
    public void Unrecognised_notes_are_returned_and_leave_no_trace_in_the_ledger()
    {
        var atm = AtmScenarios.InService();
        atm.DequeuePendingMovements();

        var result = atm.Deposit([new DepositNote(Currency.TRY, 100, 1)], unrecognizedCount: 4, DepositId, AtmScenarios.Context());

        Assert.Equal(4, result.UnrecognizedReturned);
        var movement = Assert.Single(atm.DequeuePendingMovements());
        Assert.Equal((MovementType.Deposit, 1, 0), (movement.Type, movement.NoteCountDelta, movement.ReservedCountDelta));
    }

    [Fact]
    public void Deposits_are_accepted_only_while_in_service()
    {
        var atm = AtmScenarios.Loaded();

        var exception = Assert.Throws<DomainException>(() => Deposit(atm, new DepositNote(Currency.TRY, 100, 1)));

        Assert.Equal("OPERATION_NOT_ALLOWED_IN_CURRENT_STATE", exception.Error.Code);
    }

    [Fact]
    public void Malformed_deposits_are_rejected()
    {
        var atm = AtmScenarios.InService();

        AssertRejected(() => Deposit(atm, new DepositNote(Currency.TRY, 200, 1)), "DENOMINATION_NOT_SUPPORTED");
        AssertRejected(() => Deposit(atm, new DepositNote(Currency.TRY, 100, 1), new DepositNote(Currency.TRY, 100, 2)), "DEPOSIT_INVALID");
        AssertRejected(() => Deposit(atm), "DEPOSIT_INVALID");
        AssertRejected(() => atm.Deposit([], -1, DepositId, AtmScenarios.Context()), "DEPOSIT_INVALID");
    }

    private static DepositResult Deposit(Atm atm, params DepositNote[] notes) =>
        atm.Deposit(notes, unrecognizedCount: 0, DepositId, AtmScenarios.Context());

    private static void AssertRejected(Action action, string expectedCode)
    {
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal(expectedCode, exception.Error.Code);
    }
}
