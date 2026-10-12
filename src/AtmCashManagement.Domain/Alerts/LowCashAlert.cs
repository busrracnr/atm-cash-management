using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Alerts;

/// <summary>Raised when a recycling cassette's available notes fall below 10% of its capacity (requirement B10).</summary>
public sealed record LowCashAlert(
    Guid AtmId,
    string AtmCode,
    int Slot,
    Currency Currency,
    int Denomination,
    int AvailableCount,
    int Threshold,
    DateTimeOffset OccurredAt);
