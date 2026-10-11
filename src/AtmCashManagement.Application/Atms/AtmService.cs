using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Atms;

public sealed class AtmService(IAppDbContext db, IUnitOfWork unitOfWork, IAtmRepository atms, TimeProvider clock)
{
    public async Task<AtmResponse> CreateAsync(CreateAtmRequest request, CancellationToken cancellationToken)
    {
        var atm = Atm.Create(request.Code, request.Location, clock.GetUtcNow());

        if (await db.Atms.AnyAsync(a => a.Code == atm.Code, cancellationToken))
        {
            throw new DomainException(AtmErrors.CodeAlreadyExists(atm.Code));
        }

        db.Atms.Add(atm);
        await db.SaveChangesAsync(cancellationToken);

        return AtmResponse.From(atm);
    }

    public async Task<AtmDetailsResponse> GetAsync(Guid atmId, CancellationToken cancellationToken)
    {
        var atm = await db.Atms
            .AsNoTracking()
            .Include(a => a.Cassettes).ThenInclude(c => c.Balances)
            .FirstOrDefaultAsync(a => a.Id == atmId, cancellationToken)
            ?? throw new DomainException(AtmErrors.NotFound(atmId));

        return AtmDetailsResponse.From(atm);
    }

    public async Task<IReadOnlyList<AtmResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Atms
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new AtmResponse(a.Id, a.Code, a.Location, a.Status, a.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<AtmDetailsResponse> ConfigureCassettesAsync(
        Guid atmId, ConfigureCassettesRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            atm.ConfigureCassettes(request.Cassettes.Select(c => c.ToDomain()).ToList(), clock.GetUtcNow());
            return AtmDetailsResponse.From(atm);
        }, cancellationToken);

    public Task<AtmDetailsResponse> ChangeStatusAsync(
        Guid atmId, ChangeAtmStatusRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            atm.ChangeStatus(request.Status!.Value);
            return AtmDetailsResponse.From(atm);
        }, cancellationToken);
}
