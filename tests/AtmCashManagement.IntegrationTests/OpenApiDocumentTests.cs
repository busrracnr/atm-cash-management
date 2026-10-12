using AtmCashManagement.IntegrationTests.Infrastructure;
using AtmCashManagement.IntegrationTests.Support;

namespace AtmCashManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OpenApiDocumentTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("CreateAtmRequest", "code")]
    [InlineData("ConfigureCassettesRequest", "cassettes")]
    [InlineData("LoadCashRequest", "items")]
    [InlineData("UnloadCashRequest", "slots")]
    [InlineData("ChangeAtmStatusRequest", "status")]
    [InlineData("CreateWithdrawalQuoteRequest", "cardNumber")]
    [InlineData("ConfirmWithdrawalQuoteRequest", "option")]
    [InlineData("DepositRequest", "notes")]
    public async Task Request_bodies_come_with_a_realistic_example(string schemaName, string exampleProperty)
    {
        var document = await _client.GetJsonAsync("/openapi/v1.json");

        var examples = document.GetProperty("components").GetProperty("schemas").GetProperty(schemaName).GetProperty("examples");
        Assert.True(examples[0].TryGetProperty(exampleProperty, out _));
    }

    [Fact]
    public async Task Every_operation_documents_the_optional_user_header()
    {
        var document = await _client.GetJsonAsync("/openapi/v1.json");

        var operations = document.GetProperty("paths").EnumerateObject().SelectMany(path => path.Value.EnumerateObject()).ToList();
        Assert.NotEmpty(operations);
        Assert.All(operations, operation => Assert.Contains(
            operation.Value.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "X-User"));
    }
}
