using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Withdrawals;

public static class WithdrawalErrors
{
    public static Error InvalidAmount(int amount) =>
        new("WITHDRAWAL_AMOUNT_INVALID", $"Withdrawal amount must be positive; {amount} was given.", ErrorType.Validation);
}
