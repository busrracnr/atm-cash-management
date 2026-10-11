using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Application.Cash;

/// <param name="Items">Notes loaded by hand, per recycling cassette.</param>
public sealed record LoadCashRequest([Required, MinLength(1)] IReadOnlyList<CashLoadItemRequest> Items);

/// <param name="Slot">Recycling cassette slot, 1 to 5.</param>
/// <param name="Count">Number of notes put into the cassette.</param>
public sealed record CashLoadItemRequest(
    [Range(1, CassetteConfigurationRules.CassetteCount)] int Slot,
    [Range(1, Cassette.Capacity)] int Count);

/// <param name="Slots">Cassettes to empty completely.</param>
public sealed record UnloadCashRequest([Required, MinLength(1)] IReadOnlyList<int> Slots);

/// <param name="Denominations">Notes in the whole ATM per currency/denomination, deposit cassette included.</param>
/// <param name="Totals">Cash per currency.</param>
public sealed record AtmBalanceResponse(
    Guid AtmId,
    string Code,
    AtmStatus Status,
    IReadOnlyList<DenominationBalanceResponse> Denominations,
    IReadOnlyList<CurrencyTotalResponse> Totals);

public sealed record DenominationBalanceResponse(
    Currency Currency,
    int Denomination,
    int NoteCount,
    int ReservedCount,
    decimal Amount);

/// <param name="Amount">All cash physically in the ATM, deposit cassette included.</param>
/// <param name="DispensableAmount">Unreserved cash in recycling cassettes, i.e. what can still be paid out.</param>
public sealed record CurrencyTotalResponse(Currency Currency, int NoteCount, decimal Amount, decimal DispensableAmount);
