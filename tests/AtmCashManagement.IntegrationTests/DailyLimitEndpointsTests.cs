using System.Net;
using System.Text.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DailyLimitEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reserved_but_unconfirmed_amounts_count_towards_the_limit()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var card = NewCardNumber();

        var first = await (await _client.RequestQuoteAsync(atmId, 800, "USD", card)).ReadJsonAsync();
        Assert.Equal(200, first.GetProperty("remainingDailyLimit").GetInt32());

        var rejected = await _client.RequestQuoteAsync(atmId, 300, "USD", card);
        var problem = await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "DAILY_LIMIT_EXCEEDED");
        Assert.Equal((800, 200), (problem.GetProperty("usedToday").GetInt32(), problem.GetProperty("remaining").GetInt32()));

        // Cancelling gives the reserved amount back to the limit.
        (await _client.PostAsync($"/api/withdrawal-quotes/{first.GetProperty("quoteId").GetGuid()}/cancel", null, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await _client.RequestQuoteAsync(atmId, 300, "USD", card)).StatusCode);
    }

    [Fact]
    public async Task Confirmed_withdrawals_count_towards_the_limit()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var card = NewCardNumber();
        var quoteId = await _client.CreateQuoteAsync(atmId, 1000, "USD", card);
        (await _client.ConfirmQuoteAsync(quoteId, Guid.NewGuid().ToString())).EnsureSuccessStatusCode();

        var response = await _client.RequestQuoteAsync(atmId, 100, "USD", card);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "DAILY_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task Each_card_and_currency_has_its_own_limit()
    {
        var atmId = await _client.CreateInServiceAtmAsync();
        var card = NewCardNumber();
        await _client.CreateQuoteAsync(atmId, 1000, "USD", card);

        Assert.Equal(HttpStatusCode.Created, (await _client.RequestQuoteAsync(atmId, 100, "TRY", card)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.RequestQuoteAsync(atmId, 1000, "USD", NewCardNumber())).StatusCode);
    }

    [Fact]
    public async Task The_upper_alternative_is_offered_only_within_the_remaining_limit()
    {
        var atmId = await CreateAtmWithLargeTryStockAsync();
        var card = NewCardNumber();
        var bigQuote = await _client.CreateQuoteAsync(atmId, 19_970, "TRY", card);
        (await _client.ConfirmQuoteAsync(bigQuote, Guid.NewGuid().ToString())).EnsureSuccessStatusCode();

        // 30 TRY remains: 30 cannot be paid exactly, 20 can, and the next payable amount (40) exceeds the limit.
        var quote = await (await _client.RequestQuoteAsync(atmId, 30, "TRY", card)).ReadJsonAsync();

        Assert.Equal(20, quote.GetProperty("lower").GetProperty("amount").GetInt32());
        Assert.Equal(JsonValueKind.Null, quote.GetProperty("upper").ValueKind);
        Assert.Equal(10, quote.GetProperty("remainingDailyLimit").GetInt32());
    }

    private async Task<Guid> CreateAtmWithLargeTryStockAsync()
    {
        var atmId = await _client.CreateAtmAsync();
        var cassettes = new
        {
            cassettes = new object[]
            {
                new { slot = 1, type = "DEPOSIT" },
                new { slot = 2, type = "RECYCLING", currency = "TRY", denomination = 500 },
                new { slot = 3, type = "RECYCLING", currency = "TRY", denomination = 50 },
                new { slot = 4, type = "RECYCLING", currency = "TRY", denomination = 20 },
                new { slot = 5, type = "RECYCLING", currency = "USD", denomination = 100 },
            },
        };
        (await _client.ConfigureCassettesAsync(atmId, cassettes)).EnsureSuccessStatusCode();
        var load = new { items = new[] { new { slot = 2, count = 100 }, new { slot = 3, count = 100 }, new { slot = 4, count = 100 } } };
        (await _client.LoadCashAsync(atmId, load)).EnsureSuccessStatusCode();
        (await _client.ChangeStatusAsync(atmId, "IN_SERVICE")).EnsureSuccessStatusCode();
        return atmId;
    }
}
