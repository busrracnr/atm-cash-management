namespace AtmCashManagement.Domain.Withdrawals;

public sealed record NoteQuantity(int Denomination, int Count)
{
    public int Amount => Denomination * Count;
}

/// <summary>How many notes of each denomination make up an amount.</summary>
public sealed class NoteBreakdown
{
    public NoteBreakdown(IEnumerable<NoteQuantity> notes)
    {
        Notes = notes.Where(n => n.Count > 0).OrderByDescending(n => n.Denomination).ToList();
    }

    /// <summary>Non-zero quantities, largest denomination first.</summary>
    public IReadOnlyList<NoteQuantity> Notes { get; }

    public int Amount => Notes.Sum(n => n.Amount);

    public int NoteCount => Notes.Sum(n => n.Count);

    public int CountOf(int denomination) => Notes.FirstOrDefault(n => n.Denomination == denomination)?.Count ?? 0;

    /// <summary>Same notation as the assignment, e.g. "50×2 + 20×3".</summary>
    public override string ToString() => string.Join(" + ", Notes.Select(n => $"{n.Denomination}×{n.Count}"));
}

/// <param name="Exact">Fewest-notes breakdown of the requested amount, when it can be paid exactly.</param>
/// <param name="Lower">Otherwise: the largest payable amount below the request.</param>
/// <param name="Upper">Otherwise: the smallest payable amount above the request, if within the allowed maximum.</param>
public sealed record NoteSelection(int RequestedAmount, NoteBreakdown? Exact, NoteBreakdown? Lower, NoteBreakdown? Upper)
{
    public bool IsExact => Exact is not null;
}
