using System.Net.Http.Json;
using System.Text.Json;

namespace AtmCashManagement.IntegrationTests.Support;

/// <summary>Small HTTP helpers so tests read as the business scenario they exercise.</summary>
internal static class AtmApi
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Slot 1 deposit; TRY 100, TRY 50, TRY 20 and USD 100 recycling cassettes.</summary>
    public static object StandardCassettes { get; } = new
    {
        cassettes = new object[]
        {
            new { slot = 1, type = "DEPOSIT" },
            new { slot = 2, type = "RECYCLING", currency = "TRY", denomination = 100 },
            new { slot = 3, type = "RECYCLING", currency = "TRY", denomination = 50 },
            new { slot = 4, type = "RECYCLING", currency = "TRY", denomination = 20 },
            new { slot = 5, type = "RECYCLING", currency = "USD", denomination = 100 },
        },
    };

    public static object StandardLoad { get; } = new
    {
        items = new object[]
        {
            new { slot = 2, count = 50 },
            new { slot = 3, count = 40 },
            new { slot = 4, count = 30 },
            new { slot = 5, count = 20 },
        },
    };

    public static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..16];

    public static async Task<Guid> CreateAtmAsync(this HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/atms", new { code = UniqueCode(), location = "Kadıköy, İstanbul" }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    /// <summary>Creates an ATM with the standard cassettes and load; it ends up LOADED.</summary>
    public static async Task<Guid> CreateLoadedAtmAsync(this HttpClient client)
    {
        var atmId = await client.CreateAtmAsync();
        (await client.ConfigureCassettesAsync(atmId, StandardCassettes)).EnsureSuccessStatusCode();
        (await client.LoadCashAsync(atmId, StandardLoad)).EnsureSuccessStatusCode();
        return atmId;
    }

    /// <summary>Standard cassettes and load, put in service: ready for withdrawals.</summary>
    public static async Task<Guid> CreateInServiceAtmAsync(this HttpClient client)
    {
        var atmId = await client.CreateLoadedAtmAsync();
        (await client.ChangeStatusAsync(atmId, "IN_SERVICE")).EnsureSuccessStatusCode();
        return atmId;
    }

    public const string TestCard = "4111 1111 1111 1111";

    public static Task<HttpResponseMessage> RequestQuoteAsync(
        this HttpClient client, Guid atmId, int amount, string currency = "TRY", string card = TestCard) =>
        client.PostAsJsonAsync($"/api/atms/{atmId}/withdrawal-quotes", new { cardNumber = card, currency, amount }, Ct);

    public static async Task<Guid> CreateQuoteAsync(this HttpClient client, Guid atmId, int amount, string currency = "TRY", string card = TestCard)
    {
        var response = await client.RequestQuoteAsync(atmId, amount, currency, card);
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("quoteId").GetGuid();
    }

    public static Task<HttpResponseMessage> ConfirmQuoteAsync(this HttpClient client, Guid quoteId, string idempotencyKey, string? option = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/withdrawal-quotes/{quoteId}/confirm")
        {
            Content = JsonContent.Create(new { option }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request, Ct);
    }

    public static Task<HttpResponseMessage> ConfigureCassettesAsync(this HttpClient client, Guid atmId, object body) =>
        client.PutAsJsonAsync($"/api/atms/{atmId}/cassettes", body, Ct);

    public static Task<HttpResponseMessage> LoadCashAsync(this HttpClient client, Guid atmId, object body) =>
        client.PostAsJsonAsync($"/api/atms/{atmId}/cash-loads", body, Ct);

    public static Task<HttpResponseMessage> ChangeStatusAsync(this HttpClient client, Guid atmId, string status) =>
        client.PutAsJsonAsync($"/api/atms/{atmId}/status", new { status }, Ct);

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    public static JsonElement Cassette(this JsonElement atm, int slot) =>
        atm.GetProperty("cassettes").EnumerateArray().Single(c => c.GetProperty("slot").GetInt32() == slot);
}
