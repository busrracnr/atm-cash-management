namespace AtmCashManagement.Domain.Ledger;

/// <param name="PerformedBy">User (X-User header) or system component that caused the movement.</param>
/// <param name="OccurredAt">UTC time of the movement.</param>
/// <param name="ReferenceId">Business operation the movement belongs to (quote, deposit, reconciliation).</param>
public sealed record MovementContext(string PerformedBy, DateTimeOffset OccurredAt, Guid? ReferenceId = null);
