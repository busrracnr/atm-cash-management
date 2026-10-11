using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Ledger;

namespace AtmCashManagement.Domain.Atms;

public sealed class Cassette
{
    public const int Capacity = 100;

    private readonly List<CassetteBalance> _balances = [];

    // Required by EF Core for materialisation.
    private Cassette()
    {
    }

    internal Cassette(Guid atmId, CassetteDefinition definition, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        AtmId = atmId;
        Slot = definition.Slot;
        Type = definition.Type;
        Currency = definition.Currency;
        Denomination = definition.Denomination;
    }

    public Guid Id { get; private set; }

    public Guid AtmId { get; private set; }

    public int Slot { get; private set; }

    public CassetteType Type { get; private set; }

    /// <summary>Currency a recycling cassette holds; null for the deposit cassette.</summary>
    public Currency? Currency { get; private set; }

    /// <summary>Denomination a recycling cassette holds; null for the deposit cassette.</summary>
    public int? Denomination { get; private set; }

    public IReadOnlyCollection<CassetteBalance> Balances => _balances;

    public int NoteCount => _balances.Sum(b => b.NoteCount);

    public int ReservedCount => _balances.Sum(b => b.ReservedCount);

    public int AvailableCount => NoteCount - ReservedCount;

    public int FreeCapacity => Capacity - NoteCount;

    public bool IsEmpty => NoteCount == 0;

    internal bool Matches(CassetteDefinition definition) =>
        Type == definition.Type && Currency == definition.Currency && Denomination == definition.Denomination;

    internal void Redefine(CassetteDefinition definition)
    {
        Type = definition.Type;
        Currency = definition.Currency;
        Denomination = definition.Denomination;

        // Only empty cassettes are redefined; drop the zero rows of the old contents.
        _balances.Clear();
    }

    /// <summary>The only way a balance changes: applies the delta and returns the matching ledger entry.</summary>
    internal CassetteMovement Record(
        MovementType type,
        Currency currency,
        int denomination,
        int noteCountDelta,
        int reservedCountDelta,
        MovementContext context)
    {
        var balance = _balances.SingleOrDefault(b => b.Currency == currency && b.Denomination == denomination);
        if (balance is null)
        {
            balance = new CassetteBalance(Id, currency, denomination);
            _balances.Add(balance);
        }

        balance.Apply(noteCountDelta, reservedCountDelta);

        return CassetteMovement.Create(
            AtmId, Id, Slot, type, currency, denomination, noteCountDelta, reservedCountDelta, context);
    }
}
