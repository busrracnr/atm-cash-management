using System.Net;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

/// <summary>Requirement B6: requests that arrive at the same moment, sent truly in parallel against a real database.</summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Parallel_quotes_never_reserve_the_same_note_twice()
    {
        var atmId = await CreateAtmWithTenHundredLiraNotesAsync();

        // 25 customers ask for 100 TRY at once; there are only ten 100 TRY notes.
        var responses = await Task.WhenAll(Enumerable.Range(0, 25)
            .Select(_ => _client.RequestQuoteAsync(atmId, 100, "TRY", NewCardNumber())));

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));

        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal((10, 10, 0), (
            atm.Cassette(2).GetProperty("noteCount").GetInt32(),
            atm.Cassette(2).GetProperty("reservedCount").GetInt32(),
            atm.Cassette(2).GetProperty("availableCount").GetInt32()));

        var reservations = (await _client.GetJsonAsync($"/api/atms/{atmId}/movements?cassetteSlot=2"))
            .EnumerateArray().Where(m => m.GetProperty("type").GetString() == "RESERVATION").ToList();
        Assert.Equal(10, reservations.Count);
        Assert.All(reservations, m => Assert.Equal(1, m.GetProperty("reservedCountDelta").GetInt32()));
        Assert.True((await _client.GetJsonAsync($"/api/ledger/consistency?atmId={atmId}")).GetProperty("isConsistent").GetBoolean());
    }

    [Fact]
    public async Task Parallel_confirmations_with_the_same_key_pay_once()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var quoteId = await _client.CreateQuoteAsync(atmId, 160);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _client.ConfirmQuoteAsync(quoteId, key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(Ct)));
        Assert.Single(bodies.Distinct());

        var atm = await _client.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal((49, 27), (atm.Cassette(2).GetProperty("noteCount").GetInt32(), atm.Cassette(4).GetProperty("noteCount").GetInt32()));
        var withdrawals = (await _client.GetJsonAsync($"/api/atms/{atmId}/movements"))
            .EnumerateArray().Count(m => m.GetProperty("type").GetString() == "WITHDRAWAL");
        Assert.Equal(2, withdrawals);
    }

    [Fact]
    public async Task Parallel_quotes_for_one_card_at_different_atms_respect_the_daily_limit()
    {
        var atmIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            atmIds.Add(await _client.CreateInServiceAtmAsync());
        }

        var card = NewCardNumber();

        // Six ATMs, 300 USD each, at the same moment: only three fit in the 1,000 USD limit.
        var responses = await Task.WhenAll(atmIds.Select(atmId => _client.RequestQuoteAsync(atmId, 300, "USD", card)));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "DAILY_LIMIT_EXCEEDED");
        }
    }

    private async Task<Guid> CreateAtmWithTenHundredLiraNotesAsync()
    {
        var atmId = await _client.CreateAtmAsync();
        (await _client.ConfigureCassettesAsync(atmId, StandardCassettes)).EnsureSuccessStatusCode();
        (await _client.LoadCashAsync(atmId, new { items = new[] { new { slot = 2, count = 10 } } })).EnsureSuccessStatusCode();
        (await _client.ChangeStatusAsync(atmId, "IN_SERVICE")).EnsureSuccessStatusCode();
        return atmId;
    }
}
