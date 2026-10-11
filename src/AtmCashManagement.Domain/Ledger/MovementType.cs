namespace AtmCashManagement.Domain.Ledger;

public enum MovementType
{
    Load,
    Unload,
    Withdrawal,
    Deposit,
    Reservation,
    ReservationRelease,
    ReconciliationAdjustment,
}
