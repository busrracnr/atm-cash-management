using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Withdrawals;

namespace AtmCashManagement.Application.Withdrawals;

/// <param name="CardNumber">Customer card number; spaces and dashes are allowed. Only a keyed hash is stored.</param>
/// <param name="Currency">TRY, USD or EUR.</param>
/// <param name="Amount">Requested amount.</param>
public sealed record CreateWithdrawalQuoteRequest(
    [Required, StringLength(32)] string CardNumber,
    [Required] Currency? Currency,
    [Range(1, int.MaxValue)] int Amount);

/// <param name="Option">EXACT (the default when offered), LOWER or UPPER.</param>
public sealed record ConfirmWithdrawalQuoteRequest(QuoteOption? Option);

/// <param name="Exact">Present when the amount can be paid exactly.</param>
/// <param name="Lower">Otherwise: the largest payable amount below the request.</param>
/// <param name="Upper">Otherwise: the smallest payable amount above the request, if within the daily limit.</param>
/// <param name="ExpiresAt">The offered notes are held until this time.</param>
public sealed record WithdrawalQuoteResponse(
    Guid QuoteId,
    Guid AtmId,
    QuoteStatus Status,
    Currency Currency,
    int RequestedAmount,
    NoteOfferResponse? Exact,
    NoteOfferResponse? Lower,
    NoteOfferResponse? Upper,
    DateTimeOffset ExpiresAt)
{
    /// <summary>Daily limit left for this card and currency after this quote; returned when the quote is created.</summary>
    public int? RemainingDailyLimit { get; init; }

    public static WithdrawalQuoteResponse From(WithdrawalQuote quote, DateTimeOffset now) =>
        new(quote.Id, quote.AtmId, quote.EffectiveStatus(now), quote.Currency, quote.RequestedAmount,
            Offer(quote, QuoteOption.Exact), Offer(quote, QuoteOption.Lower), Offer(quote, QuoteOption.Upper), quote.ExpiresAt);

    private static NoteOfferResponse? Offer(WithdrawalQuote quote, QuoteOption option) =>
        quote.Options.Contains(option)
            ? new NoteOfferResponse(quote.AmountOf(option), NoteCountResponse.Summarize(quote.NotesOf(option)))
            : null;
}

public sealed record NoteOfferResponse(int Amount, IReadOnlyList<NoteCountResponse> Notes);

public sealed record NoteCountResponse(int Denomination, int Count)
{
    public static IReadOnlyList<NoteCountResponse> Summarize(IEnumerable<CassetteAllocation> notes) =>
        notes
            .GroupBy(n => n.Denomination)
            .OrderByDescending(g => g.Key)
            .Select(g => new NoteCountResponse(g.Key, g.Sum(n => n.Count)))
            .ToList();
}

/// <param name="DispensedFrom">Cassettes the notes were taken from.</param>
public sealed record WithdrawalConfirmationResponse(
    Guid QuoteId,
    QuoteStatus Status,
    QuoteOption Option,
    Currency Currency,
    int Amount,
    IReadOnlyList<NoteCountResponse> Notes,
    IReadOnlyList<CassetteNotesResponse> DispensedFrom,
    DateTimeOffset ConfirmedAt)
{
    public static WithdrawalConfirmationResponse From(WithdrawalQuote quote, QuoteSettlement settlement) =>
        new(quote.Id, quote.Status, quote.ConfirmedOption!.Value, quote.Currency, quote.DispensedAmount!.Value,
            NoteCountResponse.Summarize(settlement.Dispensed),
            settlement.Dispensed.Select(d => new CassetteNotesResponse(d.Slot, d.Denomination, d.Count)).ToList(),
            quote.ClosedAt!.Value);
}

public sealed record CassetteNotesResponse(int Slot, int Denomination, int Count);
