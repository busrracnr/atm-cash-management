using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Deposits;

namespace AtmCashManagement.Application.Deposits;

/// <param name="Notes">Recognised notes per currency and denomination, as counted by the machine.</param>
/// <param name="UnrecognizedCount">Notes the machine could not recognise; they are returned and are not part of the deposit.</param>
public sealed record DepositRequest(
    [Required] IReadOnlyList<DepositNoteRequest> Notes,
    [Range(0, 10_000)] int UnrecognizedCount);

/// <param name="Currency">TRY, USD or EUR.</param>
/// <param name="Denomination">20, 50, 100 or 500.</param>
/// <param name="Count">Number of notes.</param>
public sealed record DepositNoteRequest(
    [Required] Currency? Currency,
    int Denomination,
    [Range(1, 10_000)] int Count)
{
    public DepositNote ToDomain() => new(Currency!.Value, Denomination, Count);
}

/// <param name="Placements">Where each note type went: a recycling cassette or the deposit cassette.</param>
/// <param name="Returned">Recognised notes handed back because the cassettes were full.</param>
/// <param name="UnrecognizedReturned">Unrecognised notes handed back.</param>
/// <param name="AcceptedTotals">Value accepted per currency.</param>
public sealed record DepositResponse(
    Guid DepositId,
    Guid AtmId,
    IReadOnlyList<DepositPlacementResponse> Placements,
    IReadOnlyList<ReturnedNotesResponse> Returned,
    int UnrecognizedReturned,
    IReadOnlyList<CurrencyAmountResponse> AcceptedTotals,
    DateTimeOffset ProcessedAt)
{
    public static DepositResponse From(Guid atmId, DepositResult result, DateTimeOffset processedAt) =>
        new(result.DepositId, atmId,
            result.Placements.Select(p => new DepositPlacementResponse(p.Currency, p.Denomination, p.Count, p.Slot, p.CassetteType)).ToList(),
            result.Returned.Select(r => new ReturnedNotesResponse(r.Currency, r.Denomination, r.Count)).ToList(),
            result.UnrecognizedReturned,
            result.Placements
                .GroupBy(p => p.Currency)
                .OrderBy(g => g.Key)
                .Select(g => new CurrencyAmountResponse(g.Key, g.Sum(p => p.Count * (decimal)p.Denomination)))
                .ToList(),
            processedAt);
}

public sealed record DepositPlacementResponse(Currency Currency, int Denomination, int Count, int Slot, CassetteType CassetteType);

public sealed record ReturnedNotesResponse(Currency Currency, int Denomination, int Count);

public sealed record CurrencyAmountResponse(Currency Currency, decimal Amount);
