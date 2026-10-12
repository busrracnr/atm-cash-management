using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Withdrawals;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class ReservationTests
{
    private static readonly CassetteAllocation ThreeHundreds = new(2, 100, 3);

    [Fact]
    public void Reserved_notes_stay_in_the_cassette_but_are_no_longer_available()
    {
        var atm = InServiceWithoutPendingMovements();

        atm.ReserveNotes([ThreeHundreds], AtmScenarios.Context());

        var cassette = atm.GetCassette(2);
        Assert.Equal((50, 3, 47), (cassette.NoteCount, cassette.ReservedCount, cassette.AvailableCount));
        var movement = Assert.Single(atm.DequeuePendingMovements());
        Assert.Equal((MovementType.Reservation, 0, 3), (movement.Type, movement.NoteCountDelta, movement.ReservedCountDelta));
    }

    [Fact]
    public void Dispensing_consumes_the_reservation_and_removes_the_notes()
    {
        var atm = InServiceWithoutPendingMovements();
        atm.ReserveNotes([ThreeHundreds], AtmScenarios.Context());
        atm.DequeuePendingMovements();

        atm.Dispense([ThreeHundreds], AtmScenarios.Context());

        var cassette = atm.GetCassette(2);
        Assert.Equal((47, 0), (cassette.NoteCount, cassette.ReservedCount));
        var movement = Assert.Single(atm.DequeuePendingMovements());
        Assert.Equal((MovementType.Withdrawal, -3, -3), (movement.Type, movement.NoteCountDelta, movement.ReservedCountDelta));
    }

    [Fact]
    public void Releasing_makes_the_notes_available_again()
    {
        var atm = InServiceWithoutPendingMovements();
        atm.ReserveNotes([ThreeHundreds], AtmScenarios.Context());
        atm.DequeuePendingMovements();

        atm.ReleaseReservation([ThreeHundreds], AtmScenarios.Context());

        Assert.Equal(50, atm.GetCassette(2).AvailableCount);
        Assert.Equal(MovementType.ReservationRelease, Assert.Single(atm.DequeuePendingMovements()).Type);
    }

    [Fact]
    public void Notes_can_only_be_reserved_while_in_service()
    {
        var atm = AtmScenarios.InMaintenance();

        var exception = Assert.Throws<DomainException>(() => atm.ReserveNotes([ThreeHundreds], AtmScenarios.Context()));

        Assert.Equal("OPERATION_NOT_ALLOWED_IN_CURRENT_STATE", exception.Error.Code);
    }

    [Fact]
    public void A_cassette_with_reserved_notes_cannot_be_unloaded()
    {
        var atm = AtmScenarios.InService();
        atm.ReserveNotes([ThreeHundreds], AtmScenarios.Context());
        atm.ChangeStatus(AtmStatus.Maintenance);

        var exception = Assert.Throws<DomainException>(() => atm.UnloadCash([2], AtmScenarios.Context()));

        Assert.Equal("CASSETTE_HAS_RESERVED_NOTES", exception.Error.Code);
    }

    private static Atm InServiceWithoutPendingMovements()
    {
        var atm = AtmScenarios.InService();
        atm.DequeuePendingMovements();
        return atm;
    }
}
