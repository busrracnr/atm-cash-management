using System.Net;
using System.Net.Http.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AtmLifecycleEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Atm_goes_through_the_full_lifecycle_with_a_consistent_ledger()
    {
        _client.DefaultRequestHeaders.Add("X-User", "ops.alice");
        var atmId = await _client.CreateAtmAsync();

        var configured = await (await _client.ConfigureCassettesAsync(atmId, StandardCassettes)).ReadJsonAsync();
        Assert.Equal("CONFIGURED", configured.GetProperty("status").GetString());
        Assert.Equal("DEPOSIT", configured.Cassette(1).GetProperty("type").GetString());

        var loaded = await (await _client.LoadCashAsync(atmId, StandardLoad)).ReadJsonAsync();
        Assert.Equal("LOADED", loaded.GetProperty("status").GetString());
        Assert.Equal(50, loaded.Cassette(2).GetProperty("noteCount").GetInt32());

        var inService = await (await _client.ChangeStatusAsync(atmId, "IN_SERVICE")).ReadJsonAsync();
        Assert.Equal("IN_SERVICE", inService.GetProperty("status").GetString());

        var maintenance = await (await _client.ChangeStatusAsync(atmId, "MAINTENANCE")).ReadJsonAsync();
        Assert.Equal("MAINTENANCE", maintenance.GetProperty("status").GetString());

        var unloadResponse = await _client.PostAsJsonAsync($"/api/atms/{atmId}/cash-unloads", new { slots = new[] { 5 } }, Ct);
        var unloaded = await unloadResponse.ReadJsonAsync();
        Assert.Equal(0, unloaded.Cassette(5).GetProperty("noteCount").GetInt32());

        var balance = await _client.GetJsonAsync($"/api/atms/{atmId}/balance");
        var tryTotal = Assert.Single(balance.GetProperty("totals").EnumerateArray());
        Assert.Equal("TRY", tryTotal.GetProperty("currency").GetString());
        Assert.Equal((50 * 100) + (40 * 50) + (30 * 20), tryTotal.GetProperty("amount").GetDecimal());

        var movements = await _client.GetJsonAsync($"/api/atms/{atmId}/movements");
        Assert.Equal(5, movements.GetArrayLength());
        Assert.Equal("UNLOAD", movements[0].GetProperty("type").GetString());
        Assert.All(movements.EnumerateArray(), m => Assert.Equal("ops.alice", m.GetProperty("performedBy").GetString()));

        var consistency = await _client.GetJsonAsync($"/api/ledger/consistency?atmId={atmId}");
        Assert.True(consistency.GetProperty("isConsistent").GetBoolean());
        Assert.Equal(4, consistency.GetProperty("balancesChecked").GetInt32());
    }

    [Fact]
    public async Task Skipping_the_setup_steps_is_an_invalid_state_transition()
    {
        var atmId = await _client.CreateAtmAsync();

        var response = await _client.ChangeStatusAsync(atmId, "IN_SERVICE");

        var problem = await response.AssertProblemAsync(HttpStatusCode.Conflict, "INVALID_STATE_TRANSITION");
        Assert.Equal("UNCONFIGURED", problem.GetProperty("currentStatus").GetString());
        Assert.Equal("IN_SERVICE", problem.GetProperty("requestedStatus").GetString());
    }

    [Fact]
    public async Task Loading_cash_before_configuring_is_not_allowed()
    {
        var atmId = await _client.CreateAtmAsync();

        var response = await _client.LoadCashAsync(atmId, StandardLoad);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "OPERATION_NOT_ALLOWED_IN_CURRENT_STATE");
    }

    [Fact]
    public async Task Configuration_without_a_foreign_currency_cassette_is_rejected()
    {
        var atmId = await _client.CreateAtmAsync();
        var tryOnly = new
        {
            cassettes = new object[]
            {
                new { slot = 1, type = "DEPOSIT" },
                new { slot = 2, type = "RECYCLING", currency = "TRY", denomination = 100 },
                new { slot = 3, type = "RECYCLING", currency = "TRY", denomination = 50 },
                new { slot = 4, type = "RECYCLING", currency = "TRY", denomination = 20 },
                new { slot = 5, type = "RECYCLING", currency = "TRY", denomination = 500 },
            },
        };

        var response = await _client.ConfigureCassettesAsync(atmId, tryOnly);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "FOREIGN_CURRENCY_CASSETTE_COUNT_INVALID");
    }

    [Fact]
    public async Task A_cassette_without_a_type_is_a_malformed_request()
    {
        var atmId = await _client.CreateAtmAsync();
        var body = new { cassettes = new object[] { new { slot = 1 } } };

        var response = await _client.ConfigureCassettesAsync(atmId, body);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Loading_beyond_a_cassettes_capacity_is_rejected()
    {
        var atmId = await _client.CreateLoadedAtmAsync();
        (await _client.ChangeStatusAsync(atmId, "IN_SERVICE")).EnsureSuccessStatusCode();
        (await _client.ChangeStatusAsync(atmId, "MAINTENANCE")).EnsureSuccessStatusCode();

        var response = await _client.LoadCashAsync(atmId, new { items = new[] { new { slot = 2, count = 51 } } });

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "CASSETTE_CAPACITY_EXCEEDED");
    }

    [Fact]
    public async Task Status_change_of_an_unknown_atm_returns_not_found()
    {
        var response = await _client.ChangeStatusAsync(Guid.NewGuid(), "MAINTENANCE");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "ATM_NOT_FOUND");
    }
}
