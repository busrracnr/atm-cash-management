using AtmCashManagement.Application.Atms;
using AtmCashManagement.Application.Cash;
using AtmCashManagement.Application.Ledger;

using Microsoft.Extensions.DependencyInjection;

namespace AtmCashManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AtmService>();
        services.AddScoped<CashService>();
        services.AddScoped<LedgerService>();

        return services;
    }
}
