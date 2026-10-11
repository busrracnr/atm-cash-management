using AtmCashManagement.Application.Ledger;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class LedgerController(LedgerService ledgerService) : ControllerBase
{
    /// <summary>Ledger entries (cassette movements) of an ATM, newest first.</summary>
    /// <param name="atmId">ATM identifier.</param>
    /// <param name="cassetteSlot">Optional slot filter.</param>
    /// <param name="take">Maximum number of entries, 1 to 500.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("atms/{atmId:guid}/movements")]
    [ProducesResponseType<IReadOnlyList<MovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<MovementResponse>> GetMovements(
        Guid atmId, [FromQuery] int? cassetteSlot, [FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        ledgerService.GetMovementsAsync(atmId, cassetteSlot, take, cancellationToken);

    /// <summary>Recomputes every cassette balance from its movements and reports differences from the stored balances.</summary>
    /// <param name="atmId">Optional: check a single ATM instead of the whole network.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("ledger/consistency")]
    [ProducesResponseType<LedgerConsistencyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<LedgerConsistencyResponse> CheckConsistency([FromQuery] Guid? atmId, CancellationToken cancellationToken) =>
        ledgerService.CheckConsistencyAsync(atmId, cancellationToken);
}
