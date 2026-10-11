using AtmCashManagement.Domain.Atms;

namespace AtmCashManagement.Application.Abstractions;

public interface IAtmRepository
{
    /// <summary>
    /// Loads the ATM with its cassettes and balances and locks it until the surrounding transaction ends,
    /// so cash-changing operations on the same ATM run one at a time. Throws ATM_NOT_FOUND if it does not exist.
    /// </summary>
    Task<Atm> GetForUpdateAsync(Guid atmId, CancellationToken cancellationToken);
}
