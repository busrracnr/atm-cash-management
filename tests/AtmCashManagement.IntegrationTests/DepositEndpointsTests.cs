using System.Net;
using System.Net.Http.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DepositEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_deposit_reports_where_each_note_went_and_what_was_returned()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var body = new
        {
            notes = new object[]
            {
                new { currency = "TRY", denomination = 100, count = 5 },
                new { currency = "TRY", denomination = 500, count = 2 },
                new { currency = "EUR", denomination = 50, count = 3 },
            },
            unrecognizedCount = 1,
        };

        var response = await _client.PostAsJsonAsync($"/api/atms/{atmId}/deposits", body, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deposit = await response.ReadJsonAsync();
        var placements = deposit.GetProperty("placements").EnumerateArray()
            .Select(p => $"{p.GetProperty("currency").GetString()} {p.GetProperty("denomination").GetInt32()}×{p.GetProperty("count").GetInt32()} → slot {p.GetProperty("slot").GetInt32()} {p.GetProperty("cassetteType").GetString()}")
            .ToList();
        Assert.Equal(["TRY 500×2 → slot 1 DEPOSIT", "TRY 100×5 → slot 2 RECYCLING", "EUR 50×3 → slot 1 DEPOSIT"], placements);
        Assert.Equal(0, deposit.GetProperty("returned").GetArrayLength());
        Assert.Equal(1, deposit.GetProperty("unrecognizedReturned").GetInt32());

        var balance = await _client.GetJsonAsync($"/api/atms/{atmId}/balance");
        var totals = balance.GetProperty("totals").EnumerateArray()
            .ToDictionary(t => t.GetProperty("currency").GetString()!, t => t.GetProperty("amount").GetDecimal());
        Assert.Equal(7600 + 1500, totals["TRY"]);
        Assert.Equal(150, totals["EUR"]);

        var depositId = deposit.GetProperty("depositId").GetString();
        var deposits = (await _client.GetJsonAsync($"/api/atms/{atmId}/movements"))
            .EnumerateArray().Where(m => m.GetProperty("type").GetString() == "DEPOSIT").ToList();
        Assert.Equal(3, deposits.Count);
        Assert.All(deposits, m => Assert.Equal(depositId, m.GetProperty("referenceId").GetString()));
        Assert.True((await _client.GetJsonAsync($"/api/ledger/consistency?atmId={atmId}")).GetProperty("isConsistent").GetBoolean());
    }

    [Fact]
    public async Task Notes_that_do_not_fit_are_returned()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var body = new { notes = new[] { new { currency = "TRY", denomination = 500, count = 103 } }, unrecognizedCount = 0 };

        var deposit = await (await _client.PostAsJsonAsync($"/api/atms/{atmId}/deposits", body, Ct)).ReadJsonAsync();

        var returned = Assert.Single(deposit.GetProperty("returned").EnumerateArray());
        Assert.Equal(3, returned.GetProperty("count").GetInt32());
        Assert.Equal(100, Assert.Single(deposit.GetProperty("placements").EnumerateArray()).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Deposits_are_refused_unless_the_atm_is_in_service()
    {
        var atmId = await _client.CreateLoadedAtmAsync();
        var body = new { notes = new[] { new { currency = "TRY", denomination = 100, count = 1 } }, unrecognizedCount = 0 };

        var response = await _client.PostAsJsonAsync($"/api/atms/{atmId}/deposits", body, Ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "OPERATION_NOT_ALLOWED_IN_CURRENT_STATE");
    }

    [Fact]
    public async Task Unsupported_denominations_are_rejected()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var body = new { notes = new[] { new { currency = "TRY", denomination = 200, count = 1 } }, unrecognizedCount = 0 };

        var response = await _client.PostAsJsonAsync($"/api/atms/{atmId}/deposits", body, Ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "DENOMINATION_NOT_SUPPORTED");
    }
}
