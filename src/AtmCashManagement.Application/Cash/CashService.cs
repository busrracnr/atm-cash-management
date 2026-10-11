using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Application.Atms;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Cash;

public sealed class CashService(
    IAppDbContext db,
    IUnitOfWork unitOfWork,
    IAtmRepository atms,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public Task<AtmDetailsResponse> LoadAsync(Guid atmId, LoadCashRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            atm.LoadCash(request.Items.Select(i => new CashLoadItem(i.Slot, i.Count)).ToList(), NewContext());
            return AtmDetailsResponse.From(atm);
        }, cancellationToken);

    public Task<AtmDetailsResponse> UnloadAsync(Guid atmId, UnloadCashRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            atm.UnloadCash(request.Slots, NewContext());
            return AtmDetailsResponse.From(atm);
        }, cancellationToken);

    public async Task<AtmBalanceResponse> GetBalanceAsync(Guid atmId, CancellationToken cancellationToken)
    {
        var atm = await db.Atms
            .AsNoTracking()
            .Include(a => a.Cassettes).ThenInclude(c => c.Balances)
            .FirstOrDefaultAsync(a => a.Id == atmId, cancellationToken)
            ?? throw new DomainException(AtmErrors.NotFound(atmId));

        var balances = atm.Cassettes
            .SelectMany(c => c.Balances.Select(b => (Cassette: c, Balance: b)))
            .Where(x => x.Balance.NoteCount > 0)
            .ToList();

        var denominations = balances
            .GroupBy(x => (x.Balance.Currency, x.Balance.Denomination))
            .OrderBy(g => g.Key.Currency).ThenByDescending(g => g.Key.Denomination)
            .Select(g => new DenominationBalanceResponse(
                g.Key.Currency,
                g.Key.Denomination,
                g.Sum(x => x.Balance.NoteCount),
                g.Sum(x => x.Balance.ReservedCount),
                g.Sum(x => x.Balance.NoteCount) * (decimal)g.Key.Denomination))
            .ToList();

        var totals = balances
            .GroupBy(x => x.Balance.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CurrencyTotalResponse(
                g.Key,
                g.Sum(x => x.Balance.NoteCount),
                g.Sum(x => x.Balance.NoteCount * (decimal)x.Balance.Denomination),
                g.Where(x => x.Cassette.Type == CassetteType.Recycling)
                    .Sum(x => x.Balance.AvailableCount * (decimal)x.Balance.Denomination)))
            .ToList();

        return new AtmBalanceResponse(atm.Id, atm.Code, atm.Status, denominations, totals);
    }

    private MovementContext NewContext() => new(currentUser.Name, clock.GetUtcNow());
}
