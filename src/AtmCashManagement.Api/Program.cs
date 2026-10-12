using AtmCashManagement.Api;
using AtmCashManagement.Api.Errors;
using AtmCashManagement.Api.Health;
using AtmCashManagement.Api.OpenApi;
using AtmCashManagement.Api.Users;
using AtmCashManagement.Application;
using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Infrastructure;
using AtmCashManagement.Infrastructure.Persistence;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddControllers().AddJsonOptions(options => JsonDefaults.Configure(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => JsonDefaults.Configure(options.SerializerOptions));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomization.Apply);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "ATM Cash Management API";
        document.Info.Version = "v1";
        document.Info.Description = "Cash management backend for a bank's ATM network.";
        return Task.CompletedTask;
    });
    options.AddOperationTransformer<UserHeaderOperationTransformer>();
    options.AddSchemaTransformer<RequestExamplesSchemaTransformer>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "ATM Cash Management API v1");
    options.DocumentTitle = "ATM Cash Management API";
});
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// Liveness: the process answers. Readiness: dependencies (database) answer too.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthCheckResponseWriter.WriteAsync });
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteAsync });

app.MapControllers();

await app.Services.MigrateDatabaseAsync();

await app.RunAsync();
