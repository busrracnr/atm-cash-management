using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Withdrawals;

public sealed record CassetteAllocation(int Slot, int Denomination, int Count);

/// <summary>
/// Maps a note breakdown onto recycling cassettes. When a currency/denomination sits in two cassettes,
/// the one with the higher fill ratio pays first (requirement B2); equal ratios fall back to the lower slot.
/// </summary>
public static class CassetteAllocator
{
    public static IReadOnlyList<CassetteAllocation> Allocate(
        IEnumerable<Cassette> cassettes, Currency currency, NoteBreakdown breakdown)
    {
        var recycling = cassettes.Where(c => c.Type == CassetteType.Recycling && c.Currency == currency).ToList();
        var allocations = new List<CassetteAllocation>();

        foreach (var notes in breakdown.Notes)
        {
            var remaining = notes.Count;
            var candidates = recycling
                .Where(c => c.Denomination == notes.Denomination && c.AvailableCount > 0)
                .OrderByDescending(c => c.FillRatio)
                .ThenBy(c => c.Slot);

            foreach (var cassette in candidates)
            {
                var taken = Math.Min(remaining, cassette.AvailableCount);
                allocations.Add(new CassetteAllocation(cassette.Slot, notes.Denomination, taken));
                remaining -= taken;
                if (remaining == 0)
                {
                    break;
                }
            }

            // The breakdown is computed from the same availability, so a shortfall means a programming error.
            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Not enough {currency} {notes.Denomination} notes to allocate {notes.Count}; {remaining} missing.");
            }
        }

        return allocations;
    }
}
