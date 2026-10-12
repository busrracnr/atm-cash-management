using AtmCashManagement.Application.Atms;
using AtmCashManagement.Application.Cash;
using AtmCashManagement.Application.Ledger;
using AtmCashManagement.Application.Withdrawals;

using Microsoft.Extensions.DependencyInjection;

namespace AtmCashManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AtmService>();
        services.AddScoped<CashService>();
        services.AddScoped<LedgerService>();
        services.AddScoped<OpenQuotes>();
        services.AddScoped<WithdrawalService>();

        return services;
    }
}
