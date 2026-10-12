using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Infrastructure.Outbox;
using AtmCashManagement.Infrastructure.Persistence;
using AtmCashManagement.Infrastructure.Security;
using AtmCashManagement.Infrastructure.Withdrawals;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AtmCashManagement.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Resolved lazily so test hosts can override the connection string after registration.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAtmRepository, AtmRepository>();
        services.AddScoped<ICardLock, PostgresCardLock>();

        services.AddSingleton<ICardNumberHasher>(serviceProvider =>
            new HmacCardNumberHasher(serviceProvider.GetRequiredService<IConfiguration>()[HmacCardNumberHasher.KeySetting]));

        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<QuoteExpiryService>();
        services.AddHostedService<OutboxDispatcherService>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
