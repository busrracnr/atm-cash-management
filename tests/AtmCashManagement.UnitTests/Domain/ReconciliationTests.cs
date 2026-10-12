using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Reconciliations;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class ReconciliationTests
{
    private static readonly Dictionary<Currency, decimal> Rates = new()
    {
        [Currency.TRY] = 1m,
        [Currency.USD] = 42m,
        [Currency.EUR] = 49m,
    };

    [Fact]
    public void A_count_matching_the_system_completes_without_adjustments()
    {
        var atm = MaintenanceAtmWithoutPendingMovements();

        var reconciliation = Reconcile(atm, "ops.alice");

        Assert.Equal(ReconciliationStatus.Completed, reconciliation.Status);
        Assert.Equal(0m, reconciliation.TotalDifferenceTry);
        Assert.Empty(atm.DequeuePendingMovements());
    }

    [Fact]
    public void Small_differences_are_posted_and_need_no_approval()
    {
        var atm = MaintenanceAtmWithoutPendingMovements();

        var reconciliation = Reconcile(atm, "ops.alice", slot2: 48, slot4: 31);

        Assert.Equal(ReconciliationStatus.Completed, reconciliation.Status);
        Assert.Equal(220m, reconciliation.TotalDifferenceTry);
        Assert.Equal((48, 31), (atm.GetCassette(2).NoteCount, atm.GetCassette(4).NoteCount));
        var adjustments = atm.DequeuePendingMovements();
        Assert.All(adjustments, m => Assert.Equal(MovementType.ReconciliationAdjustment, m.Type));
        Assert.Equal([-2, 1], adjustments.OrderBy(m => m.CassetteSlot).Select(m => m.NoteCountDelta));
        atm.ChangeStatus(AtmStatus.InService);
    }

    [Fact]
    public void A_total_of_exactly_the_threshold_needs_no_approval()
    {
        var reconciliation = Reconcile(AtmScenarios.InMaintenance(), "ops.alice", slot2: 40);

        Assert.Equal(1_000m, reconciliation.TotalDifferenceTry);
        Assert.Equal(ReconciliationStatus.Completed, reconciliation.Status);
    }

    [Fact]
    public void Above_the_threshold_the_atm_waits_for_a_different_user()
    {
        var atm = AtmScenarios.InMaintenance();

        var reconciliation = Reconcile(atm, "ops.alice", slot2: 48, slot5: 19);

        Assert.Equal(200m + 4_200m, reconciliation.TotalDifferenceTry);
        Assert.Equal(ReconciliationStatus.AwaitingApproval, reconciliation.Status);
        Assert.Equal(reconciliation.Id, atm.PendingReconciliationId);
        AssertRejected(() => atm.ChangeStatus(AtmStatus.InService), "RECONCILIATION_AWAITING_APPROVAL");
        AssertRejected(() => reconciliation.Approve("OPS.ALICE", AtmScenarios.Now), "FOUR_EYES_VIOLATION");

        reconciliation.Approve("ops.bob", AtmScenarios.Now);
        atm.ApproveReconciliation(reconciliation.Id);

        Assert.Equal((ReconciliationStatus.Approved, "ops.bob"), (reconciliation.Status, reconciliation.ApprovedBy));
        atm.ChangeStatus(AtmStatus.InService);
        Assert.Equal(AtmStatus.InService, atm.Status);
    }

    [Fact]
    public void A_surplus_does_not_hide_a_shortage()
    {
        var reconciliation = Reconcile(AtmScenarios.InMaintenance(), "ops.alice", slot2: 56, slot3: 28);

        Assert.Equal(0m, reconciliation.Lines.Sum(l => l.DifferenceTry));
        Assert.Equal(1_200m, reconciliation.TotalDifferenceTry);
        Assert.Equal(ReconciliationStatus.AwaitingApproval, reconciliation.Status);
    }

    [Fact]
    public void The_deposit_cassette_is_counted_per_note_type_and_valued_in_try()
    {
        var atm = AtmScenarios.InMaintenance();

        var reconciliation = Reconcile(atm, "ops.alice", depositNotes: [new CountedNote(Currency.EUR, 50, 2)]);

        var line = Assert.Single(reconciliation.Lines, l => l.Slot == 1);
        Assert.Equal((0, 2, 2, 4_900m), (line.SystemCount, line.CountedCount, line.Difference, line.DifferenceTry));
        Assert.Equal(2, atm.GetCassette(1).NoteCount);
    }

    [Fact]
    public void A_pending_reconciliation_blocks_a_new_one()
    {
        var atm = AtmScenarios.InMaintenance();
        Reconcile(atm, "ops.alice", slot5: 10);

        AssertRejected(atm.EnsureCanReconcile, "RECONCILIATION_AWAITING_APPROVAL");
    }

    [Fact]
    public void Reconciliation_is_not_allowed_while_in_service()
    {
        AssertRejected(AtmScenarios.InService().EnsureCanReconcile, "OPERATION_NOT_ALLOWED_IN_CURRENT_STATE");
    }

    [Fact]
    public void Every_cassette_must_be_counted_once_in_the_right_shape()
    {
        var atm = AtmScenarios.InMaintenance();

        AssertInvalid(atm, [new CassetteCount(1, Notes: []), new CassetteCount(2, 50), new CassetteCount(3, 40), new CassetteCount(4, 30)]);
        AssertInvalid(atm, [new CassetteCount(1, Count: 0), new CassetteCount(2, 50), new CassetteCount(3, 40), new CassetteCount(4, 30), new CassetteCount(5, 20)]);
        AssertInvalid(atm, [new CassetteCount(1, Notes: []), new CassetteCount(2, Notes: []), new CassetteCount(3, 40), new CassetteCount(4, 30), new CassetteCount(5, 20)]);
        AssertInvalid(atm, [new CassetteCount(1, Notes: []), new CassetteCount(2, 101), new CassetteCount(3, 40), new CassetteCount(4, 30), new CassetteCount(5, 20)]);
    }

    private static Reconciliation Reconcile(
        Atm atm, string countedBy, int slot2 = 50, int slot3 = 40, int slot4 = 30, int slot5 = 20, params CountedNote[] depositNotes)
    {
        var reconciliation = Reconciliation.Create(
            atm,
            [new CassetteCount(1, Notes: depositNotes), new CassetteCount(2, slot2), new CassetteCount(3, slot3), new CassetteCount(4, slot4), new CassetteCount(5, slot5)],
            Rates,
            countedBy,
            AtmScenarios.Now);
        atm.Reconcile(reconciliation, AtmScenarios.Context(countedBy));
        return reconciliation;
    }

    private static Atm MaintenanceAtmWithoutPendingMovements()
    {
        var atm = AtmScenarios.InMaintenance();
        atm.DequeuePendingMovements();
        return atm;
    }

    private static void AssertInvalid(Atm atm, List<CassetteCount> counts) =>
        AssertRejected(() => Reconciliation.Create(atm, counts, Rates, "ops.alice", AtmScenarios.Now), "RECONCILIATION_INVALID");

    private static void AssertRejected(Action action, string expectedCode)
    {
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal(expectedCode, exception.Error.Code);
    }
}
