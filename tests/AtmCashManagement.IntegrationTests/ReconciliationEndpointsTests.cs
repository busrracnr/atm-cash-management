using System.Net;
using System.Net.Http.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ReconciliationEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    public async Task Large_differences_keep_the_atm_out_of_service_until_a_second_user_approves()
    {
        var alice = ClientFor("ops.alice");
        var bob = ClientFor("ops.bob");
        var atmId = await MaintenanceAtmAsync(alice);
        var usdRate = await UsdRateAsync();

        var created = await alice.PostAsJsonAsync($"/api/atms/{atmId}/reconciliations", Counts(slot2: 48, slot5: 19), Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var reconciliation = await created.ReadJsonAsync();
        var reconciliationId = reconciliation.GetProperty("reconciliationId").GetGuid();
        Assert.Equal("AWAITING_APPROVAL", reconciliation.GetProperty("status").GetString());
        Assert.Equal(200 + (100 * usdRate), reconciliation.GetProperty("totalDifferenceTry").GetDecimal());
        Assert.Equal(reconciliationId, (await alice.GetJsonAsync($"/api/atms/{atmId}")).GetProperty("pendingReconciliationId").GetGuid());

        await (await alice.ChangeStatusAsync(atmId, "IN_SERVICE")).AssertProblemAsync(HttpStatusCode.Conflict, "RECONCILIATION_AWAITING_APPROVAL");
        await (await alice.PostAsync($"/api/reconciliations/{reconciliationId}/approve", null, Ct)).AssertProblemAsync(HttpStatusCode.Forbidden, "FOUR_EYES_VIOLATION");
        await (await _anonymous.PostAsync($"/api/reconciliations/{reconciliationId}/approve", null, Ct)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "APPROVER_REQUIRED");

        var approved = await (await bob.PostAsync($"/api/reconciliations/{reconciliationId}/approve", null, Ct)).ReadJsonAsync();

        Assert.Equal(("APPROVED", "ops.bob"), (approved.GetProperty("status").GetString(), approved.GetProperty("approvedBy").GetString()));
        (await alice.ChangeStatusAsync(atmId, "IN_SERVICE")).EnsureSuccessStatusCode();
        var atm = await alice.GetJsonAsync($"/api/atms/{atmId}");
        Assert.Equal((48, 19), (atm.Cassette(2).GetProperty("noteCount").GetInt32(), atm.Cassette(5).GetProperty("noteCount").GetInt32()));
        var adjustments = (await alice.GetJsonAsync($"/api/atms/{atmId}/movements"))
            .EnumerateArray().Count(m => m.GetProperty("type").GetString() == "RECONCILIATION_ADJUSTMENT");
        Assert.Equal(2, adjustments);
        Assert.True((await alice.GetJsonAsync($"/api/ledger/consistency?atmId={atmId}")).GetProperty("isConsistent").GetBoolean());
    }

    [Fact]
    public async Task Small_differences_are_posted_and_the_atm_can_return_to_service_at_once()
    {
        var alice = ClientFor("ops.alice");
        var atmId = await MaintenanceAtmAsync(alice);

        var reconciliation = await (await alice.PostAsJsonAsync($"/api/atms/{atmId}/reconciliations", Counts(slot4: 28), Ct)).ReadJsonAsync();

        Assert.Equal("COMPLETED", reconciliation.GetProperty("status").GetString());
        Assert.Equal(40m, reconciliation.GetProperty("totalDifferenceTry").GetDecimal());
        (await alice.ChangeStatusAsync(atmId, "IN_SERVICE")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reconciliation_is_refused_while_the_atm_is_in_service()
    {
        var atmId = await _anonymous.CreateInServiceAtmAsync();

        var response = await _anonymous.PostAsJsonAsync($"/api/atms/{atmId}/reconciliations", Counts(), Ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "OPERATION_NOT_ALLOWED_IN_CURRENT_STATE");
    }

    [Fact]
    public async Task Exchange_rates_are_seeded_and_can_be_updated()
    {
        var rates = await _anonymous.GetJsonAsync("/api/exchange-rates");
        Assert.Equal(["EUR", "TRY", "USD"], rates.EnumerateArray().Select(r => r.GetProperty("currency").GetString()).Order());
        var original = await UsdRateAsync();

        try
        {
            var updated = await (await _anonymous.PutAsJsonAsync("/api/exchange-rates/USD", new { rateToTry = 45.5m }, Ct)).ReadJsonAsync();
            Assert.Equal(45.5m, updated.GetProperty("rateToTry").GetDecimal());

            var tryRate = await _anonymous.PutAsJsonAsync("/api/exchange-rates/TRY", new { rateToTry = 2m }, Ct);
            await tryRate.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "EXCHANGE_RATE_INVALID");
        }
        finally
        {
            // Other tests value USD differences with the current rate; leave it as found.
            (await _anonymous.PutAsJsonAsync("/api/exchange-rates/USD", new { rateToTry = original }, Ct)).EnsureSuccessStatusCode();
        }
    }

    private HttpClient ClientFor(string user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User", user);
        return client;
    }

    private static async Task<Guid> MaintenanceAtmAsync(HttpClient client)
    {
        var atmId = await client.CreateInServiceAtmAsync();
        (await client.ChangeStatusAsync(atmId, "MAINTENANCE")).EnsureSuccessStatusCode();
        return atmId;
    }

    private async Task<decimal> UsdRateAsync() =>
        (await _anonymous.GetJsonAsync("/api/exchange-rates")).EnumerateArray()
            .Single(r => r.GetProperty("currency").GetString() == "USD").GetProperty("rateToTry").GetDecimal();

    /// <summary>Hand count of the standard ATM (deposit cassette empty); defaults equal the system figures.</summary>
    private static object Counts(int slot2 = 50, int slot3 = 40, int slot4 = 30, int slot5 = 20) => new
    {
        cassettes = new object[]
        {
            new { slot = 1, notes = Array.Empty<object>() },
            new { slot = 2, count = slot2 },
            new { slot = 3, count = slot3 },
            new { slot = 4, count = slot4 },
            new { slot = 5, count = slot5 },
        },
    };
}
