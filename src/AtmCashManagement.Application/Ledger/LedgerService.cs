using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Ledger;

public sealed class LedgerService(IAppDbContext db, TimeProvider clock)
{
    public const int MaxPageSize = 500;

    public async Task<IReadOnlyList<MovementResponse>> GetMovementsAsync(
        Guid atmId, int? cassetteSlot, int take, CancellationToken cancellationToken)
    {
        await EnsureAtmExistsAsync(atmId, cancellationToken);

        var query = db.CassetteMovements.AsNoTracking().Where(m => m.AtmId == atmId);
        if (cassetteSlot is not null)
        {
            query = query.Where(m => m.CassetteSlot == cassetteSlot);
        }

        return await query
            .OrderByDescending(m => m.Sequence)
            .Take(Math.Clamp(take, 1, MaxPageSize))
            .Select(m => new MovementResponse(
                m.Sequence, m.Id, m.CassetteSlot, m.Type, m.Currency, m.Denomination,
                m.NoteCountDelta, m.ReservedCountDelta, m.ReferenceId, m.PerformedBy, m.OccurredAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Recomputes every balance from the ledger and reports where it differs from the stored balance.</summary>
    public async Task<LedgerConsistencyResponse> CheckConsistencyAsync(Guid? atmId, CancellationToken cancellationToken)
    {
        if (atmId is not null)
        {
            await EnsureAtmExistsAsync(atmId.Value, cancellationToken);
        }

        var cassettes = await db.Cassettes
            .AsNoTracking()
            .Where(c => atmId == null || c.AtmId == atmId)
            .Select(c => new { c.Id, c.AtmId, c.Slot })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var stored = await db.CassetteBalances
            .AsNoTracking()
            .Where(b => atmId == null || db.Cassettes.Any(c => c.Id == b.CassetteId && c.AtmId == atmId))
            .Select(b => new { b.CassetteId, b.Currency, b.Denomination, b.NoteCount, b.ReservedCount })
            .ToListAsync(cancellationToken);

        var fromLedger = await db.CassetteMovements
            .AsNoTracking()
            .Where(m => atmId == null || m.AtmId == atmId)
            .GroupBy(m => new { m.CassetteId, m.Currency, m.Denomination })
            .Select(g => new
            {
                g.Key.CassetteId,
                g.Key.Currency,
                g.Key.Denomination,
                NoteCount = g.Sum(m => m.NoteCountDelta),
                ReservedCount = g.Sum(m => m.ReservedCountDelta),
            })
            .ToListAsync(cancellationToken);

        var storedByKey = stored.ToDictionary(b => (b.CassetteId, b.Currency, b.Denomination));
        var ledgerByKey = fromLedger.ToDictionary(l => (l.CassetteId, l.Currency, l.Denomination));

        // A balance missing on one side counts as zero there (e.g. a redefined cassette's emptied rows).
        var differences = storedByKey.Keys.Union(ledgerByKey.Keys)
            .Select(key =>
            {
                storedByKey.TryGetValue(key, out var s);
                ledgerByKey.TryGetValue(key, out var l);
                var cassette = cassettes[key.CassetteId];
                return new LedgerDifferenceResponse(
                    cassette.AtmId, cassette.Slot, key.Currency, key.Denomination,
                    s?.NoteCount ?? 0, l?.NoteCount ?? 0, s?.ReservedCount ?? 0, l?.ReservedCount ?? 0);
            })
            .Where(d => d.StoredNoteCount != d.LedgerNoteCount || d.StoredReservedCount != d.LedgerReservedCount)
            .OrderBy(d => d.AtmId).ThenBy(d => d.CassetteSlot)
            .ToList();

        return new LedgerConsistencyResponse(
            clock.GetUtcNow(), atmId, differences.Count == 0, storedByKey.Keys.Union(ledgerByKey.Keys).Count(), differences);
    }

    private async Task EnsureAtmExistsAsync(Guid atmId, CancellationToken cancellationToken)
    {
        if (!await db.Atms.AnyAsync(a => a.Id == atmId, cancellationToken))
        {
            throw new DomainException(AtmErrors.NotFound(atmId));
        }
    }
}
