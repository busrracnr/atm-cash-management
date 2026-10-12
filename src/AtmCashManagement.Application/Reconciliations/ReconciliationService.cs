using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Reconciliations;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Reconciliations;

public sealed class ReconciliationService(
    IAppDbContext db,
    IUnitOfWork unitOfWork,
    IAtmRepository atms,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public Task<ReconciliationResponse> ReconcileAsync(Guid atmId, ReconcileRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            atm.EnsureCanReconcile();

            var rates = await db.ExchangeRates.AsNoTracking().ToDictionaryAsync(r => r.Currency, r => r.RateToTry, ct);
            var now = clock.GetUtcNow();
            var reconciliation = Reconciliation.Create(atm, request.Cassettes.Select(c => c.ToDomain()).ToList(), rates, currentUser.Name, now);

            atm.Reconcile(reconciliation, new MovementContext(currentUser.Name, now, reconciliation.Id));
            db.Reconciliations.Add(reconciliation);

            return ReconciliationResponse.From(reconciliation);
        }, cancellationToken);

    public async Task<ReconciliationResponse> GetAsync(Guid reconciliationId, CancellationToken cancellationToken)
    {
        var reconciliation = await db.Reconciliations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reconciliationId, cancellationToken)
            ?? throw new DomainException(ReconciliationErrors.NotFound(reconciliationId));

        return ReconciliationResponse.From(reconciliation);
    }

    /// <summary>Second pair of eyes: a different, identified user accepts the differences and the ATM may return to service.</summary>
    public Task<ReconciliationResponse> ApproveAsync(Guid reconciliationId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var approver = currentUser.Name;
            if (approver == CurrentUserLimits.Anonymous)
            {
                throw new DomainException(ReconciliationErrors.ApproverRequired());
            }

            var atmId = await db.Reconciliations.Where(r => r.Id == reconciliationId).Select(r => (Guid?)r.AtmId).FirstOrDefaultAsync(ct)
                ?? throw new DomainException(ReconciliationErrors.NotFound(reconciliationId));
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            var reconciliation = await db.Reconciliations.SingleAsync(r => r.Id == reconciliationId, ct);

            reconciliation.Approve(approver, clock.GetUtcNow());
            atm.ApproveReconciliation(reconciliation.Id);

            return ReconciliationResponse.From(reconciliation);
        }, cancellationToken);
}
