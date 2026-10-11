using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Ledger;

/// <summary>
/// Immutable ledger entry. Every change to a cassette produces one, so a cassette's balance is the sum of its movements.
/// </summary>
public sealed class CassetteMovement
{
    private CassetteMovement()
    {
    }

    /// <summary>Database-generated, strictly increasing position in the ledger.</summary>
    public long Sequence { get; private set; }

    public Guid Id { get; private set; }

    public Guid AtmId { get; private set; }

    public Guid CassetteId { get; private set; }

    public int CassetteSlot { get; private set; }

    public MovementType Type { get; private set; }

    public Currency Currency { get; private set; }

    public int Denomination { get; private set; }

    /// <summary>Change in physical notes inside the cassette.</summary>
    public int NoteCountDelta { get; private set; }

    /// <summary>Change in notes reserved by open withdrawal quotes.</summary>
    public int ReservedCountDelta { get; private set; }

    public Guid? ReferenceId { get; private set; }

    public string PerformedBy { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    internal static CassetteMovement Create(
        Guid atmId,
        Guid cassetteId,
        int cassetteSlot,
        MovementType type,
        Currency currency,
        int denomination,
        int noteCountDelta,
        int reservedCountDelta,
        MovementContext context) =>
        new()
        {
            Id = Guid.CreateVersion7(context.OccurredAt),
            AtmId = atmId,
            CassetteId = cassetteId,
            CassetteSlot = cassetteSlot,
            Type = type,
            Currency = currency,
            Denomination = denomination,
            NoteCountDelta = noteCountDelta,
            ReservedCountDelta = reservedCountDelta,
            ReferenceId = context.ReferenceId,
            PerformedBy = context.PerformedBy,
            OccurredAt = context.OccurredAt,
        };
}
