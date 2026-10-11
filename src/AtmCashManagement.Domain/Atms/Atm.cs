using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;

namespace AtmCashManagement.Domain.Atms;

/// <summary>Aggregate root: an ATM with its five cassettes. All cassette changes go through this class.</summary>
public sealed class Atm
{
    public const int CodeMaxLength = 32;
    public const int LocationMaxLength = 200;

    private readonly List<Cassette> _cassettes = [];
    private readonly List<CassetteMovement> _pendingMovements = [];

    // Required by EF Core for materialisation.
    private Atm()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public AtmStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Cassette> Cassettes => _cassettes;

    public static Atm Create(string code, string location, DateTimeOffset createdAt)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is 0 or > CodeMaxLength)
        {
            throw new DomainException(AtmErrors.InvalidCode());
        }

        var normalizedLocation = location.Trim();
        if (normalizedLocation.Length is 0 or > LocationMaxLength)
        {
            throw new DomainException(AtmErrors.InvalidLocation());
        }

        return new Atm
        {
            // Time-ordered UUIDv7 keeps the primary-key index append-only.
            Id = Guid.CreateVersion7(createdAt),
            Code = normalizedCode,
            Location = normalizedLocation,
            Status = AtmStatus.Unconfigured,
            CreatedAt = createdAt,
        };
    }

    public Cassette GetCassette(int slot) =>
        _cassettes.SingleOrDefault(c => c.Slot == slot) ?? throw new DomainException(CassetteErrors.NotFound(slot));

    /// <summary>Ledger entries produced since the last save; persisted together with the balances.</summary>
    public IReadOnlyList<CassetteMovement> DequeuePendingMovements()
    {
        var movements = _pendingMovements.ToList();
        _pendingMovements.Clear();
        return movements;
    }

    public void ChangeStatus(AtmStatus target)
    {
        if (!AtmLifecycle.CanTransition(Status, target))
        {
            throw new DomainException(AtmErrors.InvalidStateTransition(Status, target, AtmLifecycle.AllowedTargets(Status)));
        }

        Status = target;
    }

    public void ConfigureCassettes(IReadOnlyCollection<CassetteDefinition> definitions, DateTimeOffset now)
    {
        EnsureAllowed(AtmOperation.ConfigureCassettes);
        CassetteConfigurationRules.Validate(definitions);

        // Validate every slot before changing any, so a rejected request leaves the ATM untouched.
        var changes = definitions
            .OrderBy(d => d.Slot)
            .Select(definition => (Definition: definition, Existing: _cassettes.SingleOrDefault(c => c.Slot == definition.Slot)))
            .Where(change => change.Existing is null || !change.Existing.Matches(change.Definition))
            .ToList();

        var occupied = changes.FirstOrDefault(change => change.Existing is { IsEmpty: false }).Existing;
        if (occupied is not null)
        {
            throw new DomainException(CassetteErrors.NotEmpty(occupied.Slot, occupied.NoteCount));
        }

        foreach (var (definition, existing) in changes)
        {
            if (existing is null)
            {
                _cassettes.Add(new Cassette(Id, definition, now));
            }
            else
            {
                existing.Redefine(definition);
            }
        }

        if (Status == AtmStatus.Unconfigured)
        {
            Status = AtmStatus.Configured;
        }
    }

    public void LoadCash(IReadOnlyCollection<CashLoadItem> items, MovementContext context)
    {
        EnsureAllowed(AtmOperation.LoadCash);

        if (items.Count == 0)
        {
            throw new DomainException(CassetteErrors.InvalidCashLoad("At least one cassette must be loaded."));
        }

        if (items.Select(i => i.Slot).Distinct().Count() != items.Count)
        {
            throw new DomainException(CassetteErrors.InvalidCashLoad("Each slot can appear only once in a cash load."));
        }

        var loads = items.Select(item => (Item: item, Cassette: GetCassette(item.Slot))).ToList();
        foreach (var (item, cassette) in loads)
        {
            if (cassette.Type == CassetteType.Deposit)
            {
                throw new DomainException(CassetteErrors.NotLoadable(cassette.Slot));
            }

            if (item.Count <= 0)
            {
                throw new DomainException(CassetteErrors.InvalidCashLoad($"Note count for slot {item.Slot} must be positive."));
            }

            if (item.Count > cassette.FreeCapacity)
            {
                throw new DomainException(CassetteErrors.CapacityExceeded(cassette.Slot, item.Count, cassette.FreeCapacity));
            }
        }

        foreach (var (item, cassette) in loads)
        {
            _pendingMovements.Add(cassette.Record(
                MovementType.Load, cassette.Currency!.Value, cassette.Denomination!.Value, item.Count, 0, context));
        }

        if (Status == AtmStatus.Configured)
        {
            Status = AtmStatus.Loaded;
        }
    }

    /// <summary>Empties the given cassettes completely, as when a cash-in-transit crew removes them.</summary>
    public void UnloadCash(IReadOnlyCollection<int> slots, MovementContext context)
    {
        EnsureAllowed(AtmOperation.UnloadCash);

        if (slots.Count == 0 || slots.Distinct().Count() != slots.Count)
        {
            throw new DomainException(CassetteErrors.InvalidCashUnload("Provide at least one slot, each slot only once."));
        }

        var cassettes = slots.Select(GetCassette).ToList();
        var reserved = cassettes.FirstOrDefault(c => c.ReservedCount > 0);
        if (reserved is not null)
        {
            throw new DomainException(CassetteErrors.HasReservedNotes(reserved.Slot, reserved.ReservedCount));
        }

        foreach (var cassette in cassettes)
        {
            foreach (var balance in cassette.Balances.Where(b => b.NoteCount > 0).ToList())
            {
                _pendingMovements.Add(cassette.Record(
                    MovementType.Unload, balance.Currency, balance.Denomination, -balance.NoteCount, 0, context));
            }
        }
    }

    private void EnsureAllowed(AtmOperation operation)
    {
        if (!AtmLifecycle.IsAllowed(operation, Status))
        {
            throw new DomainException(AtmErrors.OperationNotAllowed(operation, Status, AtmLifecycle.StatesAllowing(operation)));
        }
    }
}
