using System.Net;
using System.Text.Json;

namespace AtmCashManagement.IntegrationTests.Support;

internal static class ProblemAssertions
{
    /// <summary>Asserts an RFC 9457 error response carrying the expected status and errorCode; returns the body.</summary>
    public static async Task<JsonElement> AssertProblemAsync(
        this HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedErrorCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.ReadJsonAsync();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedErrorCode, problem.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));

        return problem;
    }
}
