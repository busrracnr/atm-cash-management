using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AtmCashManagement.IntegrationTests.Infrastructure;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_database_as_healthy()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        var database = Assert.Single(report.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", database.GetProperty("name").GetString());
    }
}
