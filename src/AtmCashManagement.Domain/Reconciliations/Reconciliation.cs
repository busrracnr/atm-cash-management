using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Reconciliations;

public enum ReconciliationStatus
{
    Completed,
    AwaitingApproval,
    Approved,
}

/// <summary>Counted notes of one cassette: a single count for a recycling cassette, notes per type for the deposit cassette.</summary>
public sealed record CassetteCount(int Slot, int? Count = null, IReadOnlyList<CountedNote>? Notes = null);

public sealed record CountedNote(Currency Currency, int Denomination, int Count);

/// <summary>System figure versus physical count for one currency/denomination in one cassette.</summary>
public sealed class ReconciliationLine
{
    // Required by EF Core for materialisation.
    private ReconciliationLine()
    {
    }

    internal ReconciliationLine(int slot, Currency currency, int denomination, int systemCount, int countedCount, decimal rateToTry)
    {
        Slot = slot;
        Currency = currency;
        Denomination = denomination;
        SystemCount = systemCount;
        CountedCount = countedCount;
        DifferenceTry = (countedCount - systemCount) * denomination * rateToTry;
    }

    public int Slot { get; private set; }

    public Currency Currency { get; private set; }

    public int Denomination { get; private set; }

    public int SystemCount { get; private set; }

    public int CountedCount { get; private set; }

    /// <summary>Counted minus system: negative is a shortage, positive a surplus.</summary>
    public int Difference => CountedCount - SystemCount;

    public decimal DifferenceTry { get; private set; }
}

/// <summary>
/// A hand count compared with the system (requirements A8, B9). Differences are posted at once so the books follow the
/// physical cash; when their total TRY value exceeds the threshold, a second user must approve before the ATM may
/// return to service (four-eyes principle).
/// </summary>
public sealed class Reconciliation
{
    public const decimal ApprovalThresholdTry = 1_000m;

    private readonly List<ReconciliationLine> _lines = [];

    // Required by EF Core for materialisation.
    private Reconciliation()
    {
    }

    public Guid Id { get; private set; }

    public Guid AtmId { get; private set; }

    public ReconciliationStatus Status { get; private set; }

    /// <summary>Sum of the absolute TRY values of all differences: a surplus does not hide a shortage.</summary>
    public decimal TotalDifferenceTry { get; private set; }

    public string CountedBy { get; private set; } = string.Empty;

    public DateTimeOffset CountedAt { get; private set; }

    public string? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public IReadOnlyCollection<ReconciliationLine> Lines => _lines;

    public static Reconciliation Create(
        Atm atm,
        IReadOnlyCollection<CassetteCount> counts,
        IReadOnlyDictionary<Currency, decimal> ratesToTry,
        string countedBy,
        DateTimeOffset now)
    {
        Validate(atm, counts);

        var reconciliation = new Reconciliation
        {
            Id = Guid.CreateVersion7(now),
            AtmId = atm.Id,
            CountedBy = countedBy,
            CountedAt = now,
        };

        foreach (var count in counts.OrderBy(c => c.Slot))
        {
            var cassette = atm.GetCassette(count.Slot);
            IReadOnlyList<CountedNote> counted = cassette.Type == CassetteType.Recycling
                ? [new CountedNote(cassette.Currency!.Value, cassette.Denomination!.Value, count.Count!.Value)]
                : count.Notes!;

            // Every note type the system knows about or the count found gets a line; missing on one side means zero.
            var noteTypes = cassette.Balances.Where(b => b.NoteCount > 0).Select(b => (b.Currency, b.Denomination))
                .Union(counted.Select(n => (n.Currency, n.Denomination)))
                .OrderBy(t => t.Currency).ThenByDescending(t => t.Denomination);

            foreach (var (currency, denomination) in noteTypes)
            {
                var system = cassette.Balances.FirstOrDefault(b => b.Currency == currency && b.Denomination == denomination)?.NoteCount ?? 0;
                var physical = counted.FirstOrDefault(n => n.Currency == currency && n.Denomination == denomination)?.Count ?? 0;
                reconciliation._lines.Add(new ReconciliationLine(count.Slot, currency, denomination, system, physical, ratesToTry[currency]));
            }
        }

        reconciliation.TotalDifferenceTry = reconciliation._lines.Sum(l => Math.Abs(l.DifferenceTry));
        reconciliation.Status = reconciliation.TotalDifferenceTry > ApprovalThresholdTry
            ? ReconciliationStatus.AwaitingApproval
            : ReconciliationStatus.Completed;

        return reconciliation;
    }

    public void Approve(string approver, DateTimeOffset now)
    {
        if (Status != ReconciliationStatus.AwaitingApproval)
        {
            throw new DomainException(ReconciliationErrors.NotAwaitingApproval(Id, Status));
        }

        if (string.Equals(approver, CountedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(ReconciliationErrors.FourEyesViolation(approver));
        }

        Status = ReconciliationStatus.Approved;
        ApprovedBy = approver;
        ApprovedAt = now;
    }

    private static void Validate(Atm atm, IReadOnlyCollection<CassetteCount> counts)
    {
        var slots = counts.Select(c => c.Slot).ToList();
        if (slots.Distinct().Count() != slots.Count || !atm.Cassettes.Select(c => c.Slot).Order().SequenceEqual(slots.Order()))
        {
            throw new DomainException(ReconciliationErrors.Invalid("Count every cassette exactly once (slots 1 to 5)."));
        }

        foreach (var count in counts)
        {
            var cassette = atm.GetCassette(count.Slot);
            if (cassette.Type == CassetteType.Recycling)
            {
                if (count.Count is not (>= 0 and <= Cassette.Capacity) || count.Notes is not null)
                {
                    throw new DomainException(ReconciliationErrors.Invalid(
                        $"Slot {count.Slot} is a recycling cassette: give a single 'count' between 0 and {Cassette.Capacity}."));
                }

                continue;
            }

            if (count.Count is not null || count.Notes is null)
            {
                throw new DomainException(ReconciliationErrors.Invalid(
                    $"Slot {count.Slot} is the deposit cassette: list the counted 'notes' per currency and denomination."));
            }

            if (count.Notes.Any(n => n.Count < 0 || !Denominations.IsSupported(n.Denomination))
                || count.Notes.Select(n => (n.Currency, n.Denomination)).Distinct().Count() != count.Notes.Count
                || count.Notes.Sum(n => n.Count) > Cassette.Capacity)
            {
                throw new DomainException(ReconciliationErrors.Invalid(
                    $"Deposit cassette notes must be supported denominations, listed once each, at most {Cassette.Capacity} in total."));
            }
        }
    }
}
