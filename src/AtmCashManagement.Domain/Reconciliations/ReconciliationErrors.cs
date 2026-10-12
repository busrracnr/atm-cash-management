using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Reconciliations;

public static class ReconciliationErrors
{
    public static Error Invalid(string reason) => new("RECONCILIATION_INVALID", reason, ErrorType.Validation);

    public static Error NotFound(Guid reconciliationId) =>
        new("RECONCILIATION_NOT_FOUND", $"Reconciliation '{reconciliationId}' was not found.", ErrorType.NotFound);

    public static Error AwaitingApproval(Guid reconciliationId) =>
        new("RECONCILIATION_AWAITING_APPROVAL",
            $"Reconciliation '{reconciliationId}' found differences above the approval threshold and must be approved by a second user first.",
            ErrorType.Conflict,
            new Dictionary<string, object?> { ["reconciliationId"] = reconciliationId });

    public static Error NotAwaitingApproval(Guid reconciliationId, ReconciliationStatus status) =>
        new("RECONCILIATION_NOT_AWAITING_APPROVAL",
            $"Reconciliation '{reconciliationId}' is {status.ToApiName()} and needs no approval.",
            ErrorType.Conflict);

    public static Error ApproverRequired() =>
        new("APPROVER_REQUIRED", "Approval needs an identified user: send the X-User header.", ErrorType.Validation);

    public static Error FourEyesViolation(string user) =>
        new("FOUR_EYES_VIOLATION",
            $"'{user}' entered this reconciliation and cannot approve it; a different user must approve.",
            ErrorType.Forbidden);
}
