using AtmCashManagement.Api.Users;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AtmCashManagement.Api.OpenApi;

/// <summary>Documents the optional X-User header on every operation so it can be set from Swagger UI.</summary>
internal sealed class UserHeaderOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HttpCurrentUser.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Acting user, recorded on ledger entries and used for four-eyes approval. Defaults to 'anonymous'.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });

        return Task.CompletedTask;
    }
}
