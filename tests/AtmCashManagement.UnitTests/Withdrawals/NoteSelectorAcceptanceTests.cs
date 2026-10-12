using AtmCashManagement.Domain.Withdrawals;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Withdrawals;

/// <summary>Requirement B3 acceptance tests, written exactly as the table in the assignment.</summary>
public sealed class NoteSelectorAcceptanceTests
{
    public static TheoryData<int, string, int, string> Cases() => new()
    {
        // #  Notes available           Requested  Expected result
        { 1, "100×0, 50×3, 20×10", 160, "exact: 50×2 + 20×3" },
        { 2, "50×1, 20×2", 80, "lower: 70 (50×1 + 20×1) | upper: 90 (50×1 + 20×2)" },
        { 3, "100×5, 20×10", 130, "lower: 120 (100×1 + 20×1) | upper: 140 (100×1 + 20×2)" },
        { 4, "100×2", 250, "lower: 200 (100×2) | upper: none" },
        { 5, "500×1, 100×0, 20×5", 600, "exact: 500×1 + 20×5" },
        { 6, "100×1, 50×4, 20×5", 200, "exact: 100×1 + 50×2" },
        { 7, "500×1, 100×3, 20×20", 1000, "exact: 500×1 + 100×3 + 20×10" },
        { 8, "100×3, 50×1, 20×1", 110, "lower: 100 (100×1) | upper: 120 (100×1 + 20×1)" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Pays_with_the_fewest_notes_or_offers_the_nearest_payable_amounts(
        int number, string notesAvailable, int requested, string expected)
    {
        var selection = NoteSelector.Select(NoteNotation.ParseInventory(notesAvailable), requested);

        Assert.Equal($"#{number} {expected}", $"#{number} {NoteNotation.Describe(selection)}");
    }
}
