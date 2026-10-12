using System.Globalization;

using AtmCashManagement.Domain.Withdrawals;

namespace AtmCashManagement.UnitTests.TestData;

/// <summary>Reads and writes the notation of the assignment's B3 table, so tests mirror it line by line.</summary>
internal static class NoteNotation
{
    /// <summary>"100×0, 50×3, 20×10" → { 100: 0, 50: 3, 20: 10 }.</summary>
    public static Dictionary<int, int> ParseInventory(string notation) =>
        notation
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('×'))
            .ToDictionary(
                pair => int.Parse(pair[0], CultureInfo.InvariantCulture),
                pair => int.Parse(pair[1], CultureInfo.InvariantCulture));

    /// <summary>"exact: 50×2 + 20×3" or "lower: 70 (50×1 + 20×1) | upper: 90 (50×1 + 20×2)".</summary>
    public static string Describe(NoteSelection selection) =>
        selection.Exact is not null
            ? $"exact: {selection.Exact}"
            : $"lower: {DescribeAlternative(selection.Lower)} | upper: {DescribeAlternative(selection.Upper)}";

    private static string DescribeAlternative(NoteBreakdown? breakdown) =>
        breakdown is null ? "none" : $"{breakdown.Amount} ({breakdown})";
}
