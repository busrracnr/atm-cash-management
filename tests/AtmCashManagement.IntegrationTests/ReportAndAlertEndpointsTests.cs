using System.Net;
using System.Text.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ReportAndAlertEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_quote_that_leaves_a_cassette_low_writes_an_alert_that_the_dispatcher_publishes()
    {
        var atmId = await _client.CreateInServiceAtmAsync();

        // 4,100 TRY takes 41 of the fifty 100 TRY notes, leaving 9 available: below 10% of the capacity.
        await _client.CreateQuoteAsync(atmId, 4_100, "TRY", NewCardNumber());

        var alert = Assert.Single((await _client.GetJsonAsync($"/api/alerts?atmId={atmId}")).EnumerateArray());
        Assert.Equal((2, "TRY", 100, 9, 10), (
            alert.GetProperty("slot").GetInt32(),
            alert.GetProperty("currency").GetString(),
            alert.GetProperty("denomination").GetInt32(),
            alert.GetProperty("availableCount").GetInt32(),
            alert.GetProperty("threshold").GetInt32()));
        Assert.Equal(JsonValueKind.Null, alert.GetProperty("processedAt").ValueKind);

        await factory.ProcessOutboxAsync();

        var published = Assert.Single((await _client.GetJsonAsync($"/api/alerts?atmId={atmId}")).EnumerateArray());
        Assert.NotEqual(JsonValueKind.Null, published.GetProperty("processedAt").ValueKind);
    }

    [Fact]
    public async Task No_alert_is_raised_while_enough_notes_remain()
    {
        var atmId = await _client.CreateInServiceAtmAsync();

        await _client.CreateQuoteAsync(atmId, 160, "TRY", NewCardNumber());

        Assert.Equal(0, (await _client.GetJsonAsync($"/api/alerts?atmId={atmId}")).GetArrayLength());
    }

    [Fact]
    public async Task The_operations_report_shows_reserved_and_available_notes_and_totals_in_try()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        await _client.CreateQuoteAsync(atmId, 160, "TRY", NewCardNumber());

        var report = await _client.GetJsonAsync($"/api/reports/operations?atmId={atmId}");

        var atm = Assert.Single(report.GetProperty("atms").EnumerateArray());
        var hundreds = atm.GetProperty("cassettes").EnumerateArray().Single(c => c.GetProperty("slot").GetInt32() == 2);
        Assert.Equal((50, 1, 49), (
            hundreds.GetProperty("noteCount").GetInt32(), hundreds.GetProperty("reservedCount").GetInt32(), hundreds.GetProperty("availableCount").GetInt32()));

        var totals = atm.GetProperty("totals").EnumerateArray().ToDictionary(t => t.GetProperty("currency").GetString()!);
        Assert.Equal((7600m, 160m, 7440m), (
            totals["TRY"].GetProperty("amount").GetDecimal(),
            totals["TRY"].GetProperty("reservedAmount").GetDecimal(),
            totals["TRY"].GetProperty("availableAmount").GetDecimal()));

        var usdRate = report.GetProperty("exchangeRates").EnumerateArray()
            .Single(r => r.GetProperty("currency").GetString() == "USD").GetProperty("rateToTry").GetDecimal();
        Assert.Equal(2000m * usdRate, totals["USD"].GetProperty("amountTry").GetDecimal());
        Assert.Equal(7600m + (2000m * usdRate), atm.GetProperty("totalTry").GetDecimal());
        Assert.Equal(atm.GetProperty("totalTry").GetDecimal(), report.GetProperty("totalTry").GetDecimal());
    }

    [Fact]
    public async Task The_network_report_adds_up_every_atm()
    {
        await _client.CreateInServiceAtmAsync();
        await _client.CreateInServiceAtmAsync();

        var report = await _client.GetJsonAsync("/api/reports/operations");

        var atms = report.GetProperty("atms").EnumerateArray().ToList();
        Assert.True(atms.Count >= 2);
        Assert.Equal(atms.Sum(a => a.GetProperty("totalTry").GetDecimal()), report.GetProperty("totalTry").GetDecimal());
    }

    [Fact]
    public async Task The_report_of_an_unknown_atm_returns_not_found()
    {
        var response = await _client.GetAsync($"/api/reports/operations?atmId={Guid.NewGuid()}", Ct);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "ATM_NOT_FOUND");
    }
}
