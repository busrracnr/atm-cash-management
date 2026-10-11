using System.Net;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using Npgsql;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class LedgerEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Consistency_check_reports_a_balance_that_no_longer_matches_its_movements()
    {
        var atmId = await _client.CreateLoadedAtmAsync();

        // Simulate a bug or manual edit that changes a stored balance without a ledger entry.
        await ExecuteSqlAsync(
            "UPDATE cassette_balances SET note_count = note_count - 1 " +
            "WHERE cassette_id = (SELECT id FROM cassettes WHERE atm_id = @atmId AND slot = 2)",
            atmId);

        var report = await _client.GetJsonAsync($"/api/ledger/consistency?atmId={atmId}");

        Assert.False(report.GetProperty("isConsistent").GetBoolean());
        var difference = Assert.Single(report.GetProperty("differences").EnumerateArray());
        Assert.Equal(2, difference.GetProperty("cassetteSlot").GetInt32());
        Assert.Equal(49, difference.GetProperty("storedNoteCount").GetInt32());
        Assert.Equal(50, difference.GetProperty("ledgerNoteCount").GetInt32());
    }

    [Theory]
    [InlineData("UPDATE cassette_movements SET note_count_delta = 0 WHERE atm_id = @atmId")]
    [InlineData("DELETE FROM cassette_movements WHERE atm_id = @atmId")]
    public async Task Ledger_entries_cannot_be_changed_even_with_direct_sql(string sql)
    {
        var atmId = await _client.CreateLoadedAtmAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(sql, atmId));

        Assert.Contains("append-only", exception.MessageText);
    }

    [Fact]
    public async Task Movements_can_be_filtered_by_cassette_slot()
    {
        var atmId = await _client.CreateLoadedAtmAsync();

        var movements = await _client.GetJsonAsync($"/api/atms/{atmId}/movements?cassetteSlot=5");

        var movement = Assert.Single(movements.EnumerateArray());
        Assert.Equal("LOAD", movement.GetProperty("type").GetString());
        Assert.Equal("USD", movement.GetProperty("currency").GetString());
        Assert.Equal(20, movement.GetProperty("noteCountDelta").GetInt32());
        Assert.Equal("anonymous", movement.GetProperty("performedBy").GetString());
    }

    [Fact]
    public async Task Movements_of_an_unknown_atm_return_not_found()
    {
        var response = await _client.GetAsync($"/api/atms/{Guid.NewGuid()}/movements", Ct);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "ATM_NOT_FOUND");
    }

    private async Task ExecuteSqlAsync(string sql, Guid atmId)
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("atmId", atmId);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
