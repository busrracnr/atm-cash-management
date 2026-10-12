using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Reconciliations;

namespace AtmCashManagement.Application.Reconciliations;

/// <param name="Cassettes">The hand count of every cassette.</param>
public sealed record ReconcileRequest([Required] IReadOnlyList<CassetteCountRequest> Cassettes);

/// <param name="Slot">Cassette slot, 1 to 5.</param>
/// <param name="Count">Recycling cassette: number of notes counted.</param>
/// <param name="Notes">Deposit cassette: notes counted per currency and denomination.</param>
public sealed record CassetteCountRequest(
    [Range(1, CassetteConfigurationRules.CassetteCount)] int Slot,
    [Range(0, Cassette.Capacity)] int? Count,
    IReadOnlyList<CountedNoteRequest>? Notes)
{
    public CassetteCount ToDomain() => new(Slot, Count, Notes?.Select(n => new CountedNote(n.Currency!.Value, n.Denomination, n.Count)).ToList());
}

public sealed record CountedNoteRequest(
    [Required] Currency? Currency,
    int Denomination,
    [Range(0, Cassette.Capacity)] int Count);

/// <param name="TotalDifferenceTry">Sum of the absolute TRY values of all differences.</param>
/// <param name="ApprovalThresholdTry">Above this total a second user must approve.</param>
public sealed record ReconciliationResponse(
    Guid ReconciliationId,
    Guid AtmId,
    ReconciliationStatus Status,
    decimal TotalDifferenceTry,
    decimal ApprovalThresholdTry,
    string CountedBy,
    DateTimeOffset CountedAt,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    IReadOnlyList<ReconciliationLineResponse> Lines)
{
    public static ReconciliationResponse From(Reconciliation reconciliation) =>
        new(reconciliation.Id, reconciliation.AtmId, reconciliation.Status, reconciliation.TotalDifferenceTry,
            Reconciliation.ApprovalThresholdTry, reconciliation.CountedBy, reconciliation.CountedAt,
            reconciliation.ApprovedBy, reconciliation.ApprovedAt,
            reconciliation.Lines
                .OrderBy(l => l.Slot).ThenBy(l => l.Currency).ThenByDescending(l => l.Denomination)
                .Select(l => new ReconciliationLineResponse(l.Slot, l.Currency, l.Denomination, l.SystemCount, l.CountedCount, l.Difference, l.DifferenceTry))
                .ToList());
}

/// <param name="Difference">Counted minus system: negative is a shortage, positive a surplus.</param>
public sealed record ReconciliationLineResponse(
    int Slot,
    Currency Currency,
    int Denomination,
    int SystemCount,
    int CountedCount,
    int Difference,
    decimal DifferenceTry);
