using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Withdrawals;

public enum QuoteStatus
{
    Open,
    Confirmed,
    Cancelled,
    Expired,
}

public enum QuoteOption
{
    Exact,
    Lower,
    Upper,
}

/// <summary>Notes an option takes from one cassette.</summary>
public sealed class QuoteLine
{
    // Required by EF Core for materialisation.
    private QuoteLine()
    {
    }

    internal QuoteLine(QuoteOption option, int slot, int denomination, int count)
    {
        Option = option;
        Slot = slot;
        Denomination = denomination;
        Count = count;
    }

    public QuoteOption Option { get; private set; }

    public int Slot { get; private set; }

    public int Denomination { get; private set; }

    public int Count { get; private set; }
}

/// <param name="Dispensed">Notes paid out, per cassette.</param>
/// <param name="Released">Reserved notes that are no longer needed, per cassette.</param>
public sealed record QuoteSettlement(IReadOnlyList<CassetteAllocation> Dispensed, IReadOnlyList<CassetteAllocation> Released);

/// <summary>
/// A withdrawal offer that holds the offered notes for a short time (requirement B4). When the request cannot be paid
/// exactly, both alternatives are reserved: per cassette the larger of the two, since only one of them will be taken.
/// </summary>
public sealed class WithdrawalQuote
{
    public const int CardHashLength = 64;
    public static readonly TimeSpan Validity = TimeSpan.FromSeconds(60);

    private readonly List<QuoteLine> _lines = [];

    // Required by EF Core for materialisation.
    private WithdrawalQuote()
    {
    }

    public Guid Id { get; private set; }

    public Guid AtmId { get; private set; }

    /// <summary>Keyed hash of the card number; the number itself is never stored.</summary>
    public string CardHash { get; private set; } = string.Empty;

    public Currency Currency { get; private set; }

    public int RequestedAmount { get; private set; }

    public QuoteStatus Status { get; private set; }

    /// <summary>Amount counted against the card's daily limit: the largest offer while open, the paid amount once confirmed.</summary>
    public int LimitAmount { get; private set; }

    public QuoteOption? ConfirmedOption { get; private set; }

    public int? DispensedAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Calendar day the quote counts towards for the daily limit.</summary>
    public DateOnly BusinessDate { get; private set; }

    public IReadOnlyCollection<QuoteLine> Lines => _lines;

    public IReadOnlyList<QuoteOption> Options => _lines.Select(l => l.Option).Distinct().Order().ToList();

    public static WithdrawalQuote Open(Atm atm, string cardHash, Currency currency, NoteSelection selection, DateTimeOffset now)
    {
        var offers = new List<(QuoteOption Option, NoteBreakdown Breakdown)>();
        if (selection.Exact is not null)
        {
            offers.Add((QuoteOption.Exact, selection.Exact));
        }

        if (selection.Lower is not null)
        {
            offers.Add((QuoteOption.Lower, selection.Lower));
        }

        if (selection.Upper is not null)
        {
            offers.Add((QuoteOption.Upper, selection.Upper));
        }

        if (offers.Count == 0)
        {
            throw new DomainException(WithdrawalErrors.InsufficientCash(currency, selection.RequestedAmount));
        }

        var quote = new WithdrawalQuote
        {
            Id = Guid.CreateVersion7(now),
            AtmId = atm.Id,
            CardHash = cardHash,
            Currency = currency,
            RequestedAmount = selection.RequestedAmount,
            Status = QuoteStatus.Open,
            LimitAmount = offers.Max(o => o.Breakdown.Amount),
            CreatedAt = now,
            ExpiresAt = now + Validity,
            BusinessDate = BusinessDay.Of(now),
        };

        foreach (var (option, breakdown) in offers)
        {
            quote._lines.AddRange(CassetteAllocator.Allocate(atm.Cassettes, currency, breakdown)
                .Select(a => new QuoteLine(option, a.Slot, a.Denomination, a.Count)));
        }

        return quote;
    }

    public int AmountOf(QuoteOption option) => _lines.Where(l => l.Option == option).Sum(l => l.Denomination * l.Count);

    public IReadOnlyList<CassetteAllocation> NotesOf(QuoteOption option) =>
        _lines.Where(l => l.Option == option).Select(l => new CassetteAllocation(l.Slot, l.Denomination, l.Count)).ToList();

    /// <summary>Notes held per cassette: enough for whichever option the customer picks.</summary>
    public IReadOnlyList<CassetteAllocation> Reservation() =>
        _lines
            .GroupBy(l => (l.Slot, l.Denomination))
            .Select(g => new CassetteAllocation(g.Key.Slot, g.Key.Denomination, g.Max(l => l.Count)))
            .OrderBy(a => a.Slot)
            .ToList();

    public bool IsExpiredAt(DateTimeOffset now) => Status == QuoteStatus.Open && now >= ExpiresAt;

    /// <summary>Status as a client should see it: an open quote past its deadline is expired even before it is swept.</summary>
    public QuoteStatus EffectiveStatus(DateTimeOffset now) => IsExpiredAt(now) ? QuoteStatus.Expired : Status;

    public QuoteSettlement Confirm(QuoteOption? requested, DateTimeOffset now)
    {
        EnsureOpen(now);

        var option = requested ?? (Options.Contains(QuoteOption.Exact)
            ? QuoteOption.Exact
            : throw new DomainException(WithdrawalErrors.OptionRequired(Options)));

        if (!Options.Contains(option))
        {
            throw new DomainException(WithdrawalErrors.OptionNotOffered(option, Options));
        }

        var dispensed = NotesOf(option);
        var released = Reservation()
            .Select(r => r with { Count = r.Count - dispensed.Where(d => d.Slot == r.Slot).Sum(d => d.Count) })
            .Where(r => r.Count > 0)
            .ToList();

        Status = QuoteStatus.Confirmed;
        ConfirmedOption = option;
        DispensedAmount = AmountOf(option);
        LimitAmount = DispensedAmount.Value;
        ClosedAt = now;

        return new QuoteSettlement(dispensed, released);
    }

    /// <summary>Cancels the quote and returns the notes to release. Cancelling a cancelled quote changes nothing.</summary>
    public IReadOnlyList<CassetteAllocation> Cancel(DateTimeOffset now)
    {
        if (Status == QuoteStatus.Cancelled)
        {
            return [];
        }

        EnsureOpen(now);
        return Close(QuoteStatus.Cancelled, now);
    }

    /// <summary>Releases an open quote regardless of its deadline, e.g. when the ATM goes into maintenance.</summary>
    public IReadOnlyList<CassetteAllocation> Withdraw(DateTimeOffset now) =>
        Status == QuoteStatus.Open ? Close(QuoteStatus.Cancelled, now) : [];

    public IReadOnlyList<CassetteAllocation> Expire(DateTimeOffset now) =>
        IsExpiredAt(now) ? Close(QuoteStatus.Expired, now) : [];

    private IReadOnlyList<CassetteAllocation> Close(QuoteStatus status, DateTimeOffset now)
    {
        var released = Reservation();
        Status = status;
        LimitAmount = 0;
        ClosedAt = now;
        return released;
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        if (IsExpiredAt(now) || Status == QuoteStatus.Expired)
        {
            throw new DomainException(WithdrawalErrors.QuoteExpired(Id, ExpiresAt));
        }

        if (Status != QuoteStatus.Open)
        {
            throw new DomainException(WithdrawalErrors.QuoteNotOpen(Id, Status));
        }
    }
}
