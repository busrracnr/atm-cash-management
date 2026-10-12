using System.Text.Json;

using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Application.Atms;
using AtmCashManagement.Application.Common;
using AtmCashManagement.Application.Outbox;
using AtmCashManagement.Domain.Alerts;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Reports;

public sealed class ReportService(IAppDbContext db, TimeProvider clock)
{
    public const int MaxAlerts = 500;

    /// <summary>Per ATM and cassette: notes, reserved and available; totals per currency and overall in TRY (B10).</summary>
    public async Task<OperationsReportResponse> GetOperationsReportAsync(Guid? atmId, CancellationToken cancellationToken)
    {
        var atms = await db.Atms
            .AsNoTracking()
            .Include(a => a.Cassettes).ThenInclude(c => c.Balances)
            .Where(a => atmId == null || a.Id == atmId)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        if (atmId is not null && atms.Count == 0)
        {
            throw new DomainException(AtmErrors.NotFound(atmId.Value));
        }

        var rates = await db.ExchangeRates.AsNoTracking().ToDictionaryAsync(r => r.Currency, r => r.RateToTry, cancellationToken);

        var reports = atms.Select(atm =>
        {
            var totals = Totals(atm.Cassettes, rates);
            return new AtmReportResponse(
                atm.Id, atm.Code, atm.Location, atm.Status,
                atm.Cassettes.OrderBy(c => c.Slot).Select(c => new CassetteReportResponse(
                    c.Slot, c.Type, c.Currency, c.Denomination, c.NoteCount, c.ReservedCount, c.AvailableCount, c.IsLowOnCash,
                    CassetteResponse.From(c).Contents)).ToList(),
                totals,
                totals.Sum(t => t.AmountTry));
        }).ToList();

        var networkTotals = Totals(atms.SelectMany(a => a.Cassettes), rates);

        return new OperationsReportResponse(
            clock.GetUtcNow(),
            reports,
            networkTotals,
            networkTotals.Sum(t => t.AmountTry),
            rates.OrderBy(r => r.Key).Select(r => new ExchangeRateUsed(r.Key, r.Value)).ToList());
    }

    /// <summary>Low-cash alerts from the outbox, newest first; ProcessedAt shows when the dispatcher published them.</summary>
    public async Task<IReadOnlyList<AlertResponse>> GetAlertsAsync(Guid? atmId, int take, CancellationToken cancellationToken)
    {
        var messages = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Type == OutboxMessage.LowCashAlertType && (atmId == null || m.AtmId == atmId))
            .OrderByDescending(m => m.OccurredAt)
            .Take(Math.Clamp(take, 1, MaxAlerts))
            .ToListAsync(cancellationToken);

        return messages.Select(m =>
        {
            var alert = JsonSerializer.Deserialize<LowCashAlert>(m.Payload, JsonDefaults.Options)!;
            return new AlertResponse(m.Id, alert.AtmId, alert.AtmCode, alert.Slot, alert.Currency, alert.Denomination,
                alert.AvailableCount, alert.Threshold, m.OccurredAt, m.ProcessedAt);
        }).ToList();
    }

    private static List<CurrencyReportTotal> Totals(IEnumerable<Cassette> cassettes, IReadOnlyDictionary<Currency, decimal> rates) =>
        cassettes
            .SelectMany(c => c.Balances)
            .Where(b => b.NoteCount > 0)
            .GroupBy(b => b.Currency)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var amount = g.Sum(b => b.NoteCount * (decimal)b.Denomination);
                var reserved = g.Sum(b => b.ReservedCount * (decimal)b.Denomination);
                return new CurrencyReportTotal(
                    g.Key, g.Sum(b => b.NoteCount), g.Sum(b => b.ReservedCount), amount, reserved, amount - reserved, amount * rates[g.Key]);
            })
            .ToList();
}
