using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Atms;

public sealed class AtmService(IAppDbContext db, TimeProvider clock)
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

    public async Task<AtmResponse> GetAsync(Guid atmId, CancellationToken cancellationToken)
    {
        var atm = await db.Atms.AsNoTracking().FirstOrDefaultAsync(a => a.Id == atmId, cancellationToken)
            ?? throw new DomainException(AtmErrors.NotFound(atmId));

        return AtmResponse.From(atm);
    }

    public async Task<IReadOnlyList<AtmResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Atms
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new AtmResponse(a.Id, a.Code, a.Location, a.Status, a.CreatedAt))
            .ToListAsync(cancellationToken);
}
