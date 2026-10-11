using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Ledger;

namespace AtmCashManagement.Application.Ledger;

public sealed record MovementResponse(
    long Sequence,
    Guid Id,
    int CassetteSlot,
    MovementType Type,
    Currency Currency,
    int Denomination,
    int NoteCountDelta,
    int ReservedCountDelta,
    Guid? ReferenceId,
    string PerformedBy,
    DateTimeOffset OccurredAt);

/// <param name="IsConsistent">True when every stored balance equals the sum of its movements.</param>
/// <param name="BalancesChecked">Number of cassette/currency/denomination balances compared.</param>
public sealed record LedgerConsistencyResponse(
    DateTimeOffset CheckedAt,
    Guid? AtmId,
    bool IsConsistent,
    int BalancesChecked,
    IReadOnlyList<LedgerDifferenceResponse> Differences);

public sealed record LedgerDifferenceResponse(
    Guid AtmId,
    int CassetteSlot,
    Currency Currency,
    int Denomination,
    int StoredNoteCount,
    int LedgerNoteCount,
    int StoredReservedCount,
    int LedgerReservedCount);
