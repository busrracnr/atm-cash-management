using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Ledger;

namespace AtmCashManagement.Application.Deposits;

public sealed class DepositService(
    IUnitOfWork unitOfWork,
    IAtmRepository atms,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public Task<DepositResponse> DepositAsync(Guid atmId, DepositRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            var now = clock.GetUtcNow();
            var depositId = Guid.CreateVersion7(now);

            // The deposit id is the ledger reference, so every DEPOSIT entry can be traced back to this request.
            var result = atm.Deposit(
                request.Notes.Select(n => n.ToDomain()).ToList(),
                request.UnrecognizedCount,
                depositId,
                new MovementContext(currentUser.Name, now, depositId));

            return DepositResponse.From(atm.Id, result, now);
        }, cancellationToken);
}
