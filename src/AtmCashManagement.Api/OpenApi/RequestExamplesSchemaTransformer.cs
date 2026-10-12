using System.Text.Json.Nodes;

using AtmCashManagement.Application.Atms;
using AtmCashManagement.Application.Cash;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AtmCashManagement.Api.OpenApi;

/// <summary>Realistic request bodies, so "Try it out" in Swagger UI starts from a valid request instead of "string".</summary>
internal sealed class RequestExamplesSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly Dictionary<Type, string> Examples = new()
    {
        [typeof(CreateAtmRequest)] = """
            { "code": "IST-KDK-001", "location": "Kadıköy Rıhtım, İstanbul" }
            """,
        [typeof(ConfigureCassettesRequest)] = """
            {
              "cassettes": [
                { "slot": 1, "type": "DEPOSIT" },
                { "slot": 2, "type": "RECYCLING", "currency": "TRY", "denomination": 100 },
                { "slot": 3, "type": "RECYCLING", "currency": "TRY", "denomination": 50 },
                { "slot": 4, "type": "RECYCLING", "currency": "TRY", "denomination": 20 },
                { "slot": 5, "type": "RECYCLING", "currency": "USD", "denomination": 100 }
              ]
            }
            """,
        [typeof(LoadCashRequest)] = """
            {
              "items": [
                { "slot": 2, "count": 50 },
                { "slot": 3, "count": 40 },
                { "slot": 4, "count": 30 },
                { "slot": 5, "count": 20 }
              ]
            }
            """,
        [typeof(UnloadCashRequest)] = """
            { "slots": [5] }
            """,
        [typeof(ChangeAtmStatusRequest)] = """
            { "status": "IN_SERVICE" }
            """,
    };

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // Parsed per call: a JsonNode can belong to only one parent.
        if (Examples.TryGetValue(context.JsonTypeInfo.Type, out var example))
        {
            schema.Examples = [JsonNode.Parse(example)!];
        }

        return Task.CompletedTask;
    }
}
