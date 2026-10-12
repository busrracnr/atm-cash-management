using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Deposits;

/// <summary>Recognised notes of one type inserted by the customer.</summary>
public sealed record DepositNote(Currency Currency, int Denomination, int Count);

/// <summary>Notes of one type that went into one cassette.</summary>
public sealed record DepositPlacement(Currency Currency, int Denomination, int Count, int Slot, CassetteType CassetteType);

/// <summary>Recognised notes handed back because no cassette had room for them.</summary>
public sealed record DepositReturn(Currency Currency, int Denomination, int Count);

public sealed record DepositResult(
    Guid DepositId,
    IReadOnlyList<DepositPlacement> Placements,
    IReadOnlyList<DepositReturn> Returned,
    int UnrecognizedReturned);
