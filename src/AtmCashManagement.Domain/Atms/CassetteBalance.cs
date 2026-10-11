using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Atms;

/// <summary>
/// Stored balance of one currency/denomination inside a cassette. A recycling cassette has a single balance;
/// the deposit cassette has one per currency/denomination it has received.
/// </summary>
public sealed class CassetteBalance
{
    // Required by EF Core for materialisation.
    private CassetteBalance()
    {
    }

    internal CassetteBalance(Guid cassetteId, Currency currency, int denomination)
    {
        CassetteId = cassetteId;
        Currency = currency;
        Denomination = denomination;
    }

    public Guid CassetteId { get; private set; }

    public Currency Currency { get; private set; }

    public int Denomination { get; private set; }

    public int NoteCount { get; private set; }

    public int ReservedCount { get; private set; }

    public int AvailableCount => NoteCount - ReservedCount;

    internal void Apply(int noteCountDelta, int reservedCountDelta)
    {
        var noteCount = NoteCount + noteCountDelta;
        var reservedCount = ReservedCount + reservedCountDelta;

        // Callers validate first; reaching this means a bug, not bad input.
        if (noteCount < 0 || reservedCount < 0 || reservedCount > noteCount)
        {
            throw new InvalidOperationException(
                $"Balance invariant violated for {Currency} {Denomination}: notes {noteCount}, reserved {reservedCount}.");
        }

        NoteCount = noteCount;
        ReservedCount = reservedCount;
    }
}
