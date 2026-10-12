using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Withdrawals;

/// <summary>
/// Finds the breakdown that pays an amount with the fewest notes, given a limited number of each note
/// (bounded change-making, solved with dynamic programming). Picking the largest note first is not
/// enough: with 50×3 and 20×10, greedy takes three 50s for 160 and gets stuck, while 50×2 + 20×3 works.
/// </summary>
public static class NoteSelector
{
    // Every supported denomination is a multiple of 10, so amounts are handled in units of 10.
    private const int Unit = 10;

    /// <param name="availableNotes">Dispensable notes per denomination (for one currency).</param>
    /// <param name="amount">Requested amount.</param>
    /// <param name="maxUpperAmount">Upper alternatives above this amount are not offered (e.g. the remaining daily limit).</param>
    public static NoteSelection Select(IReadOnlyDictionary<int, int> availableNotes, int amount, int? maxUpperAmount = null)
    {
        if (amount <= 0)
        {
            throw new DomainException(WithdrawalErrors.InvalidAmount(amount));
        }

        var table = new ChangeTable(availableNotes);

        if (amount % Unit == 0 && table.IsPayable(amount / Unit))
        {
            return new NoteSelection(amount, table.Breakdown(amount / Unit), null, null);
        }

        var lower = Enumerable.Range(1, Math.Min(table.MaxUnits, (amount - 1) / Unit))
            .Reverse()
            .Where(table.IsPayable)
            .Select(units => table.Breakdown(units))
            .FirstOrDefault();

        var upperLimitUnits = Math.Min(table.MaxUnits, (maxUpperAmount ?? int.MaxValue) / Unit);
        var firstUpperUnits = (amount / Unit) + 1;
        var upper = firstUpperUnits > upperLimitUnits
            ? null
            : Enumerable.Range(firstUpperUnits, upperLimitUnits - firstUpperUnits + 1)
                .Where(table.IsPayable)
                .Select(units => table.Breakdown(units))
                .FirstOrDefault();

        return new NoteSelection(amount, null, lower, upper);
    }

    /// <summary>For every amount up to the total stock: the fewest notes that pay it and how they are made up.</summary>
    private sealed class ChangeTable
    {
        private const int NotPayable = int.MaxValue;

        private readonly (int Denomination, int Count)[] _stock;
        private readonly int[] _fewestNotes;
        private readonly int[,] _notesTaken;

        public ChangeTable(IReadOnlyDictionary<int, int> availableNotes)
        {
            foreach (var (denomination, count) in availableNotes)
            {
                if (!Denominations.IsSupported(denomination) || count < 0)
                {
                    throw new ArgumentException($"Invalid stock entry {denomination}×{count}.", nameof(availableNotes));
                }
            }

            // Smallest first, so the largest denomination is decided first when a breakdown is read back.
            _stock = availableNotes
                .Where(n => n.Value > 0)
                .Select(n => (n.Key, n.Value))
                .OrderBy(n => n.Key)
                .ToArray();

            MaxUnits = _stock.Sum(n => n.Denomination / Unit * n.Count);
            _fewestNotes = new int[MaxUnits + 1];
            _notesTaken = new int[_stock.Length, MaxUnits + 1];

            Array.Fill(_fewestNotes, NotPayable);
            _fewestNotes[0] = 0;

            for (var stage = 0; stage < _stock.Length; stage++)
            {
                AddDenomination(stage);
            }
        }

        public int MaxUnits { get; }

        public bool IsPayable(int units) => units <= MaxUnits && _fewestNotes[units] != NotPayable;

        public NoteBreakdown Breakdown(int units)
        {
            var notes = new List<NoteQuantity>();
            for (var stage = _stock.Length - 1; stage >= 0; stage--)
            {
                var taken = _notesTaken[stage, units];
                notes.Add(new NoteQuantity(_stock[stage].Denomination, taken));
                units -= taken * (_stock[stage].Denomination / Unit);
            }

            return new NoteBreakdown(notes);
        }

        // fewest[s] = min over k ≤ count of (fewest using the smaller notes)[s − k·step] + k.
        private void AddDenomination(int stage)
        {
            var (denomination, count) = _stock[stage];
            var step = denomination / Unit;
            var previous = (int[])_fewestNotes.Clone();

            for (var units = 0; units <= MaxUnits; units++)
            {
                var fewest = NotPayable;
                var taken = 0;
                for (var k = 0; k <= Math.Min(count, units / step); k++)
                {
                    var rest = previous[units - (k * step)];

                    // "<=" keeps the larger k on a tie: among equally short breakdowns, prefer bigger notes.
                    if (rest != NotPayable && rest + k <= fewest)
                    {
                        fewest = rest + k;
                        taken = k;
                    }
                }

                _fewestNotes[units] = fewest;
                _notesTaken[stage, units] = taken;
            }
        }
    }
}
