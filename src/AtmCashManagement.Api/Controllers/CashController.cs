using AtmCashManagement.Application.Atms;
using AtmCashManagement.Application.Cash;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api/atms/{atmId:guid}")]
public sealed class CashController(CashService cashService) : ControllerBase
{
    /// <summary>
    /// Records cash loaded by hand into recycling cassettes (each holds at most 100 notes).
    /// Allowed while CONFIGURED (moves to LOADED) or MAINTENANCE.
    /// </summary>
    [HttpPost("cash-loads")]
    [ProducesResponseType<AtmDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<AtmDetailsResponse> Load(Guid atmId, LoadCashRequest request, CancellationToken cancellationToken) =>
        cashService.LoadAsync(atmId, request, cancellationToken);

    /// <summary>Empties the given cassettes completely. Allowed while MAINTENANCE.</summary>
    [HttpPost("cash-unloads")]
    [ProducesResponseType<AtmDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<AtmDetailsResponse> Unload(Guid atmId, UnloadCashRequest request, CancellationToken cancellationToken) =>
        cashService.UnloadAsync(atmId, request, cancellationToken);

    /// <summary>Notes per denomination and total balance per currency, available at any time.</summary>
    [HttpGet("balance")]
    [ProducesResponseType<AtmBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AtmBalanceResponse> GetBalance(Guid atmId, CancellationToken cancellationToken) =>
        cashService.GetBalanceAsync(atmId, cancellationToken);
}
