using System.Net;
using System.Net.Http.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

using static AtmCashManagement.IntegrationTests.Support.AtmApi;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AtmEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Created_atm_is_persisted_in_unconfigured_state()
    {
        var code = UniqueCode();

        var createResponse = await _client.PostAsJsonAsync("/api/atms", new { code, location = "Kadıköy, İstanbul" }, Ct);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var atm = await _client.GetJsonAsync(createResponse.Headers.Location!.ToString());
        Assert.Equal(code.ToUpperInvariant(), atm.GetProperty("code").GetString());
        Assert.Equal("UNCONFIGURED", atm.GetProperty("status").GetString());
        Assert.Equal(0, atm.GetProperty("cassettes").GetArrayLength());
    }

    [Fact]
    public async Task Duplicate_code_returns_conflict_problem_details()
    {
        var request = new { code = UniqueCode(), location = "Beşiktaş, İstanbul" };
        (await _client.PostAsJsonAsync("/api/atms", request, Ct)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/atms", request, Ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "ATM_CODE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Unknown_atm_returns_not_found_problem_details()
    {
        var response = await _client.GetAsync($"/api/atms/{Guid.NewGuid()}", Ct);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "ATM_NOT_FOUND");
    }

    [Fact]
    public async Task Missing_fields_return_validation_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/atms", new { code = "" }, Ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }
}
