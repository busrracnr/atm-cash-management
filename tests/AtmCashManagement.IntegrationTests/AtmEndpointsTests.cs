using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AtmEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Created_atm_is_persisted_in_unconfigured_state()
    {
        var code = UniqueCode();

        var createResponse = await _client.PostAsJsonAsync("/api/atms", new { code, location = "Kadıköy, İstanbul" }, Ct);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var atm = await _client.GetFromJsonAsync<JsonElement>(createResponse.Headers.Location, Ct);
        Assert.Equal(code.ToUpperInvariant(), atm.GetProperty("code").GetString());
        Assert.Equal("UNCONFIGURED", atm.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Duplicate_code_returns_conflict_problem_details()
    {
        var request = new { code = UniqueCode(), location = "Beşiktaş, İstanbul" };
        (await _client.PostAsJsonAsync("/api/atms", request, Ct)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/atms", request, Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "ATM_CODE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Unknown_atm_returns_not_found_problem_details()
    {
        var response = await _client.GetAsync($"/api/atms/{Guid.NewGuid()}", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "ATM_NOT_FOUND");
    }

    [Fact]
    public async Task Missing_fields_return_validation_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/atms", new { code = "" }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..16];

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedErrorCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedErrorCode, problem.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }
}
