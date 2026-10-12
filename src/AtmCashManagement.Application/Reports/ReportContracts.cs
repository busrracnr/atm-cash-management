using AtmCashManagement.Application.Atms;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Application.Reports;

/// <param name="Totals">Per currency across all ATMs in the report.</param>
/// <param name="TotalTry">All cash in the report valued in TRY with <paramref name="ExchangeRates"/>.</param>
public sealed record OperationsReportResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AtmReportResponse> Atms,
    IReadOnlyList<CurrencyReportTotal> Totals,
    decimal TotalTry,
    IReadOnlyList<ExchangeRateUsed> ExchangeRates);

public sealed record AtmReportResponse(
    Guid AtmId,
    string Code,
    string Location,
    AtmStatus Status,
    IReadOnlyList<CassetteReportResponse> Cassettes,
    IReadOnlyList<CurrencyReportTotal> Totals,
    decimal TotalTry);

/// <param name="LowOnCash">Recycling cassette with fewer than 10% of its capacity available.</param>
public sealed record CassetteReportResponse(
    int Slot,
    CassetteType Type,
    Currency? Currency,
    int? Denomination,
    int NoteCount,
    int ReservedCount,
    int AvailableCount,
    bool LowOnCash,
    IReadOnlyList<NoteBalanceResponse> Contents);

/// <param name="Amount">All cash of the currency, reserved notes included.</param>
/// <param name="AmountTry">Amount valued in TRY.</param>
public sealed record CurrencyReportTotal(
    Currency Currency,
    int NoteCount,
    int ReservedCount,
    decimal Amount,
    decimal ReservedAmount,
    decimal AvailableAmount,
    decimal AmountTry);

public sealed record ExchangeRateUsed(Currency Currency, decimal RateToTry);

public sealed record AlertResponse(
    Guid AlertId,
    Guid AtmId,
    string AtmCode,
    int Slot,
    Currency Currency,
    int Denomination,
    int AvailableCount,
    int Threshold,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ProcessedAt);
