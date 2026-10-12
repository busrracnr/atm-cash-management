using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Deposits;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Withdrawals;

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

    /// <summary>Unreserved notes per denomination across the recycling cassettes of a currency.</summary>
    public IReadOnlyDictionary<int, int> AvailableNotes(Currency currency) =>
        _cassettes
            .Where(c => c.Type == CassetteType.Recycling && c.Currency == currency)
            .GroupBy(c => c.Denomination!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.AvailableCount));

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

    /// <summary>Holds notes for a withdrawal quote; they stay in the cassette but can no longer be offered.</summary>
    public void ReserveNotes(IReadOnlyCollection<CassetteAllocation> notes, MovementContext context)
    {
        EnsureAllowed(AtmOperation.Withdraw);

        var reservations = notes.Select(n => (Notes: n, Cassette: GetRecyclingCassette(n))).ToList();
        foreach (var (allocation, cassette) in reservations)
        {
            // The quote was computed from this availability under the same lock; a shortfall is a bug.
            if (allocation.Count > cassette.AvailableCount)
            {
                throw new InvalidOperationException(
                    $"Cannot reserve {allocation.Count} notes in slot {allocation.Slot}; {cassette.AvailableCount} available.");
            }
        }

        foreach (var (allocation, cassette) in reservations)
        {
            _pendingMovements.Add(cassette.Record(
                MovementType.Reservation, cassette.Currency!.Value, allocation.Denomination, 0, allocation.Count, context));
        }
    }

    /// <summary>Pays out reserved notes: they leave the cassette and their reservation is consumed.</summary>
    public void Dispense(IReadOnlyCollection<CassetteAllocation> notes, MovementContext context)
    {
        EnsureAllowed(AtmOperation.Withdraw);

        foreach (var allocation in notes)
        {
            var cassette = GetRecyclingCassette(allocation);
            _pendingMovements.Add(cassette.Record(
                MovementType.Withdrawal, cassette.Currency!.Value, allocation.Denomination, -allocation.Count, -allocation.Count, context));
        }
    }

    /// <summary>Gives reserved notes back to the pool; allowed in any state (e.g. when switching to maintenance).</summary>
    public void ReleaseReservation(IReadOnlyCollection<CassetteAllocation> notes, MovementContext context)
    {
        foreach (var allocation in notes)
        {
            var cassette = GetRecyclingCassette(allocation);
            _pendingMovements.Add(cassette.Record(
                MovementType.ReservationRelease, cassette.Currency!.Value, allocation.Denomination, 0, -allocation.Count, context));
        }
    }

    /// <summary>
    /// Routes inserted notes (requirements A11, B7): a matching recycling cassette first, the deposit cassette when there is
    /// no match or the recycling cassette is full, and back to the customer when the deposit cassette is full too.
    /// Larger notes are placed first, so if space runs out the customer gets back the smallest value.
    /// </summary>
    public DepositResult Deposit(
        IReadOnlyCollection<DepositNote> notes, int unrecognizedCount, Guid depositId, MovementContext context)
    {
        EnsureAllowed(AtmOperation.Deposit);
        ValidateDeposit(notes, unrecognizedCount);

        var depositCassette = _cassettes.Single(c => c.Type == CassetteType.Deposit);
        var placements = new List<DepositPlacement>();
        var returned = new List<DepositReturn>();

        foreach (var note in notes.OrderByDescending(n => n.Denomination).ThenBy(n => n.Currency))
        {
            var remaining = note.Count;

            // With two matching cassettes, the emptier one takes notes first, keeping them balanced.
            var targets = _cassettes
                .Where(c => c.Type == CassetteType.Recycling && c.Currency == note.Currency && c.Denomination == note.Denomination)
                .OrderByDescending(c => c.FreeCapacity)
                .ThenBy(c => c.Slot)
                .Append(depositCassette);

            foreach (var cassette in targets)
            {
                var placed = Math.Min(remaining, cassette.FreeCapacity);
                if (placed == 0)
                {
                    continue;
                }

                _pendingMovements.Add(cassette.Record(MovementType.Deposit, note.Currency, note.Denomination, placed, 0, context));
                placements.Add(new DepositPlacement(note.Currency, note.Denomination, placed, cassette.Slot, cassette.Type));
                remaining -= placed;
                if (remaining == 0)
                {
                    break;
                }
            }

            if (remaining > 0)
            {
                returned.Add(new DepositReturn(note.Currency, note.Denomination, remaining));
            }
        }

        return new DepositResult(depositId, placements, returned, unrecognizedCount);
    }

    private static void ValidateDeposit(IReadOnlyCollection<DepositNote> notes, int unrecognizedCount)
    {
        if (unrecognizedCount < 0)
        {
            throw new DomainException(DepositErrors.Invalid("The number of unrecognised notes cannot be negative."));
        }

        if (notes.Count == 0 && unrecognizedCount == 0)
        {
            throw new DomainException(DepositErrors.Invalid("A deposit needs at least one note."));
        }

        if (notes.Any(n => n.Count <= 0))
        {
            throw new DomainException(DepositErrors.Invalid("Each note count must be positive."));
        }

        var unsupported = notes.FirstOrDefault(n => !Denominations.IsSupported(n.Denomination));
        if (unsupported is not null)
        {
            throw new DomainException(CassetteErrors.UnsupportedDenomination(unsupported.Denomination));
        }

        if (notes.Select(n => (n.Currency, n.Denomination)).Distinct().Count() != notes.Count)
        {
            throw new DomainException(DepositErrors.Invalid("Each currency and denomination can appear only once per deposit."));
        }
    }

    private Cassette GetRecyclingCassette(CassetteAllocation allocation)
    {
        var cassette = GetCassette(allocation.Slot);
        if (cassette.Type != CassetteType.Recycling || cassette.Denomination != allocation.Denomination)
        {
            throw new InvalidOperationException($"Slot {allocation.Slot} does not hold {allocation.Denomination} notes.");
        }

        return cassette;
    }

    public void EnsureAllowed(AtmOperation operation)
    {
        if (!AtmLifecycle.IsAllowed(operation, Status))
        {
            throw new DomainException(AtmErrors.OperationNotAllowed(operation, Status, AtmLifecycle.StatesAllowing(operation)));
        }
    }
}
