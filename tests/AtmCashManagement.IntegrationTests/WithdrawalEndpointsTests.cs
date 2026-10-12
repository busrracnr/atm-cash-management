using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using Npgsql;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class WithdrawalEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_quote_reserves_notes_and_confirming_it_pays_them_out()
    {
        var atmId = await _client.CreateInServiceAtmAsync();

        var quoteResponse = await _client.RequestQuoteAsync(atmId, 160);

        Assert.Equal(HttpStatusCode.Created, quoteResponse.StatusCode);
        var quote = await quoteResponse.ReadJsonAsync();
        Assert.Equal("OPEN", quote.GetProperty("status").GetString());
        Assert.Equal(160, quote.GetProperty("exact").GetProperty("amount").GetInt32());
        Assert.Equal(JsonValueKind.Null, quote.GetProperty("lower").ValueKind);
        var reserved = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal((1, 3), (reserved.Cassette(2).GetProperty("reservedCount").GetInt32(), reserved.Cassette(4).GetProperty("reservedCount").GetInt32()));

        var confirmation = await _client.ConfirmQuoteAsync(quote.GetProperty("quoteId").GetGuid(), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var paid = await confirmation.ReadJsonAsync();
        Assert.Equal(("CONFIRMED", "EXACT", 160), (paid.GetProperty("status").GetString(), paid.GetProperty("option").GetString(), paid.GetProperty("amount").GetInt32()));
        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal((49, 0), (atm.Cassette(2).GetProperty("noteCount").GetInt32(), atm.Cassette(2).GetProperty("reservedCount").GetInt32()));
        Assert.Equal((27, 0), (atm.Cassette(4).GetProperty("noteCount").GetInt32(), atm.Cassette(4).GetProperty("reservedCount").GetInt32()));
        await AssertLedgerConsistentAsync(atmId);
    }

    [Fact]
    public async Task Repeating_a_confirmation_with_the_same_key_returns_the_same_result_and_pays_once()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 160);
        var key = Guid.NewGuid().ToString();

        var first = await _client.ConfirmQuoteAsync(quoteId, key);
        var second = await _client.ConfirmQuoteAsync(quoteId, key);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await second.Content.ReadAsStringAsync(Ct));
        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal(49, atm.Cassette(2).GetProperty("noteCount").GetInt32());
        var movements = await _client.GetJsonAsync($"/api/atms/{atmId}/movements");
        Assert.Equal(2, movements.EnumerateArray().Count(m => m.GetProperty("type").GetString() == "WITHDRAWAL"));
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var key = Guid.NewGuid().ToString();
        (await _client.ConfirmQuoteAsync(await _client.CreateQuoteAsync(atmId, 160), key)).EnsureSuccessStatusCode();

        var response = await _client.ConfirmQuoteAsync(await _client.CreateQuoteAsync(atmId, 100), key);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Confirming_without_an_idempotency_key_is_a_malformed_request()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 160);

        var response = await _client.PostAsJsonAsync($"/api/withdrawal-quotes/{quoteId}/confirm", new { }, Ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task When_the_amount_cannot_be_paid_exactly_the_chosen_alternative_is_paid()
    {
        var atmId = await _client.CreateInServiceAtmAsync();

        var quote = await (await _client.RequestQuoteAsync(atmId, 30)).ReadJsonAsync();

        Assert.Equal(JsonValueKind.Null, quote.GetProperty("exact").ValueKind);
        Assert.Equal(20, quote.GetProperty("lower").GetProperty("amount").GetInt32());
        Assert.Equal(40, quote.GetProperty("upper").GetProperty("amount").GetInt32());

        var paid = await (await _client.ConfirmQuoteAsync(quote.GetProperty("quoteId").GetGuid(), Guid.NewGuid().ToString(), "UPPER")).ReadJsonAsync();

        Assert.Equal(40, paid.GetProperty("amount").GetInt32());
        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal((28, 0), (atm.Cassette(4).GetProperty("noteCount").GetInt32(), atm.Cassette(4).GetProperty("reservedCount").GetInt32()));
        await AssertLedgerConsistentAsync(atmId);
    }

    [Fact]
    public async Task A_cancelled_quote_releases_its_notes_and_cannot_be_confirmed()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 160);

        var cancelled = await (await _client.PostAsync($"/api/withdrawal-quotes/{quoteId}/cancel", null, Ct)).ReadJsonAsync();

        Assert.Equal("CANCELLED", cancelled.GetProperty("status").GetString());
        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal(0, atm.Cassette(2).GetProperty("reservedCount").GetInt32());
        var response = await _client.ConfirmQuoteAsync(quoteId, Guid.NewGuid().ToString());
        await response.AssertProblemAsync(HttpStatusCode.Conflict, "QUOTE_NOT_OPEN");
    }

    [Fact]
    public async Task An_expired_quote_releases_its_notes_and_cannot_be_confirmed()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 160);

        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await factory.ExpireDueQuotesAsync();

        var quote = await _client.GetJsonAsync($"/api/withdrawal-quotes/{quoteId}");
        Assert.Equal("EXPIRED", quote.GetProperty("status").GetString());
        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal(0, atm.Cassette(2).GetProperty("reservedCount").GetInt32());
        var response = await _client.ConfirmQuoteAsync(quoteId, Guid.NewGuid().ToString());
        await response.AssertProblemAsync(HttpStatusCode.Conflict, "QUOTE_EXPIRED");
        await AssertLedgerConsistentAsync(atmId);
    }

    [Fact]
    public async Task Switching_to_maintenance_cancels_open_quotes()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 160);

        (await _client.ChangeStatusAsync(atmId, "MAINTENANCE")).EnsureSuccessStatusCode();

        var quote = await _client.GetJsonAsync($"/api/withdrawal-quotes/{quoteId}");
        Assert.Equal("CANCELLED", quote.GetProperty("status").GetString());
        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.All(atm.GetProperty("cassettes").EnumerateArray(), c => Assert.Equal(0, c.GetProperty("reservedCount").GetInt32()));
    }

    [Fact]
    public async Task Withdrawals_are_refused_unless_the_atm_is_in_service()
    {
        var atmId = await _client.CreateLoadedAtmAsync();

        var response = await _client.RequestQuoteAsync(atmId, 100);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "OPERATION_NOT_ALLOWED_IN_CURRENT_STATE");
    }

    [Fact]
    public async Task A_currency_without_notes_cannot_be_withdrawn()
    {
        var atmId = await _client.CreateInServiceAtmAsync();

        var response = await _client.RequestQuoteAsync(atmId, 100, "EUR");

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_CASH");
    }

    [Fact]
    public async Task An_invalid_card_number_is_rejected()
    {
        var atmId = await _client.CreateInServiceAtmAsync();

        var response = await _client.RequestQuoteAsync(atmId, 100, card: "4111 1111 1111 1112");

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "CARD_NUMBER_INVALID");
    }

    [Fact]
    public async Task The_card_number_is_stored_only_as_a_keyed_hash()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 100);

        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT card_hash FROM withdrawal_quotes WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", quoteId);
        var stored = (string)(await command.ExecuteScalarAsync(Ct))!;

        var keyedHash = Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("integration-test-card-hash-key-0123456789"), Encoding.UTF8.GetBytes("4111111111111111")));
        var plainHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("4111111111111111")));
        Assert.Equal(keyedHash, stored);
        Assert.NotEqual(plainHash, stored);
    }

    private async Task AssertLedgerConsistentAsync(Guid atmId)
    {
        var report = await _client.GetJsonAsync($"/api/ledger/consistency?atmId={atmId}");
        Assert.True(report.GetProperty("isConsistent").GetBoolean());
    }
}
