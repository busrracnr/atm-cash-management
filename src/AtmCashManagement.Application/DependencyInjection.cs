using AtmCashManagement.Application.Atms;
using AtmCashManagement.Application.Cash;
using AtmCashManagement.Application.Deposits;
using AtmCashManagement.Application.ExchangeRates;
using AtmCashManagement.Application.Ledger;
using AtmCashManagement.Application.Reconciliations;
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
        services.AddScoped<DepositService>();
        services.AddScoped<ReconciliationService>();
        services.AddScoped<ExchangeRateService>();

        return services;
    }
}
