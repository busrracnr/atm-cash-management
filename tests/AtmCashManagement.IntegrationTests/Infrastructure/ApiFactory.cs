using AtmCashManagement.Application.Withdrawals;
using AtmCashManagement.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using Testcontainers.PostgreSql;

namespace AtmCashManagement.IntegrationTests.Infrastructure;

/// <summary>Boots the real API against a throwaway PostgreSQL container; migrations run exactly as in production.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as docker-compose.yml, so it is pulled only once.
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Controllable clock: tests move time forward instead of waiting, e.g. for a quote to expire.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>For tests that must bypass the API, e.g. to simulate tampering with stored data.</summary>
    public string DatabaseConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>Runs the quote-expiry sweep now instead of waiting for the background service.</summary>
    public async Task<int> ExpireDueQuotesAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WithdrawalService>().ExpireDueQuotesAsync(CancellationToken.None);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = _database.GetConnectionString(),
                ["Security:CardHashKey"] = "integration-test-card-hash-key-0123456789",
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
