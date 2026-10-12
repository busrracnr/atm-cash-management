using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Withdrawals;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Withdrawals;

public sealed class NoteSelectorTests
{
    private static readonly int[] Denominations = [500, 100, 50, 20];

    [Theory]
    [InlineData(89, "lower: 70 (50×1 + 20×1) | upper: none")]
    [InlineData(90, "lower: 70 (50×1 + 20×1) | upper: 90 (50×1 + 20×2)")]
    public void Upper_alternative_is_offered_only_within_the_allowed_maximum(int maxUpperAmount, string expected)
    {
        var selection = NoteSelector.Select(NoteNotation.ParseInventory("50×1, 20×2"), 80, maxUpperAmount);

        Assert.Equal(expected, NoteNotation.Describe(selection));
    }

    [Fact]
    public void An_amount_that_is_not_a_multiple_of_ten_gets_the_surrounding_alternatives()
    {
        var selection = NoteSelector.Select(NoteNotation.ParseInventory("100×5, 20×10"), 135);

        Assert.Equal("lower: 120 (100×1 + 20×1) | upper: 140 (100×1 + 20×2)", NoteNotation.Describe(selection));
    }

    [Fact]
    public void A_request_above_the_whole_stock_offers_the_largest_payable_amount()
    {
        var selection = NoteSelector.Select(NoteNotation.ParseInventory("100×2, 20×1"), 1000);

        Assert.Equal("lower: 220 (100×2 + 20×1) | upper: none", NoteNotation.Describe(selection));
    }

    [Fact]
    public void An_empty_stock_offers_nothing()
    {
        var selection = NoteSelector.Select(new Dictionary<int, int>(), 100);

        Assert.Equal("lower: none | upper: none", NoteNotation.Describe(selection));
    }

    [Fact]
    public void Exact_payment_takes_precedence_over_alternatives()
    {
        var selection = NoteSelector.Select(NoteNotation.ParseInventory("50×1, 20×3"), 60);

        Assert.True(selection.IsExact);
        Assert.Null(selection.Lower);
        Assert.Null(selection.Upper);
        Assert.Equal("20×3", selection.Exact!.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void Non_positive_amounts_are_rejected(int amount)
    {
        var exception = Assert.Throws<DomainException>(() => NoteSelector.Select(NoteNotation.ParseInventory("100×1"), amount));

        Assert.Equal("WITHDRAWAL_AMOUNT_INVALID", exception.Error.Code);
    }

    [Fact]
    public void Unsupported_denominations_in_the_stock_are_a_programming_error()
    {
        Assert.Throws<ArgumentException>(() => NoteSelector.Select(new Dictionary<int, int> { [200] = 1 }, 200));
    }

    /// <summary>
    /// Compares the algorithm with exhaustive search on 400 random stocks and requests, so correctness does not
    /// rest on eight hand-picked examples. The seed is fixed: a failure is reproducible.
    /// </summary>
    [Fact]
    public void Matches_exhaustive_search_on_random_stocks()
    {
        var random = new Random(20261012);

        for (var run = 0; run < 400; run++)
        {
            var stock = Denominations.ToDictionary(d => d, _ => random.Next(0, 3) == 0 ? 0 : random.Next(0, 7));
            var fewestNotesByAmount = ExhaustiveSearch(stock);
            var total = stock.Sum(n => n.Key * n.Value);
            var amount = (random.Next(1, (total / 10) + 10) * 10) + (random.Next(4) == 0 ? random.Next(1, 10) : 0);
            int? maxUpper = random.Next(2) == 0 ? null : amount + random.Next(0, 600);

            var selection = NoteSelector.Select(stock, amount, maxUpper);

            var context = $"run {run}: stock {string.Join(", ", stock.Select(n => $"{n.Key}×{n.Value}"))}, amount {amount}, max upper {maxUpper}";
            Assert.True(
                Summarize(Expected(fewestNotesByAmount, amount, maxUpper)) == Summarize(selection),
                $"{context}: expected {Summarize(Expected(fewestNotesByAmount, amount, maxUpper))}, got {Summarize(selection)}");

            foreach (var breakdown in new[] { selection.Exact, selection.Lower, selection.Upper }.OfType<NoteBreakdown>())
            {
                Assert.All(breakdown.Notes, n => Assert.True(n.Count <= stock[n.Denomination], $"{context}: uses unavailable notes"));
            }
        }
    }

    private static Dictionary<int, int> ExhaustiveSearch(Dictionary<int, int> stock)
    {
        var fewest = new Dictionary<int, int>();
        for (var a = 0; a <= stock[500]; a++)
        {
            for (var b = 0; b <= stock[100]; b++)
            {
                for (var c = 0; c <= stock[50]; c++)
                {
                    for (var d = 0; d <= stock[20]; d++)
                    {
                        var amount = (500 * a) + (100 * b) + (50 * c) + (20 * d);
                        var notes = a + b + c + d;
                        if (amount > 0 && (!fewest.TryGetValue(amount, out var known) || notes < known))
                        {
                            fewest[amount] = notes;
                        }
                    }
                }
            }
        }

        return fewest;
    }

    private static (int? Exact, int? Lower, int? Upper, int? Notes, int? LowerNotes, int? UpperNotes) Expected(
        Dictionary<int, int> fewestNotesByAmount, int amount, int? maxUpper)
    {
        if (fewestNotesByAmount.TryGetValue(amount, out var notes))
        {
            return (amount, null, null, notes, null, null);
        }

        int? lower = fewestNotesByAmount.Keys.Where(a => a < amount).DefaultIfEmpty().Max() is var l and > 0 ? l : null;
        int? upper = fewestNotesByAmount.Keys.Where(a => a > amount && a <= (maxUpper ?? int.MaxValue)).DefaultIfEmpty().Min() is var u and > 0 ? u : null;

        return (null, lower, upper, null,
            lower is null ? null : fewestNotesByAmount[lower.Value],
            upper is null ? null : fewestNotesByAmount[upper.Value]);
    }

    private static string Summarize((int? Exact, int? Lower, int? Upper, int? Notes, int? LowerNotes, int? UpperNotes) e) =>
        $"exact {e.Exact}/{e.Notes}, lower {e.Lower}/{e.LowerNotes}, upper {e.Upper}/{e.UpperNotes}";

    private static string Summarize(NoteSelection s) =>
        Summarize((s.Exact?.Amount, s.Lower?.Amount, s.Upper?.Amount, s.Exact?.NoteCount, s.Lower?.NoteCount, s.Upper?.NoteCount));
}
