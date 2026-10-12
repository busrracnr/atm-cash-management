using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Deposits;

public static class DepositErrors
{
    public static Error Invalid(string reason) => new("DEPOSIT_INVALID", reason, ErrorType.Validation);
}
