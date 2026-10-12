using AtmCashManagement.Application.Deposits;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api/atms/{atmId:guid}/deposits")]
public sealed class DepositsController(DepositService depositService) : ControllerBase
{
    /// <summary>
    /// Records a customer deposit. Notes with a matching recycling cassette go there, others (and the overflow of a full
    /// recycling cassette) go to the deposit cassette; what does not fit, and every unrecognised note, is returned.
    /// Allowed while the ATM is IN_SERVICE.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DepositResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<DepositResponse> Deposit(Guid atmId, DepositRequest request, CancellationToken cancellationToken) =>
        depositService.DepositAsync(atmId, request, cancellationToken);
}
