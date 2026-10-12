using AtmCashManagement.Application.Reconciliations;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ReconciliationsController(ReconciliationService reconciliationService) : ControllerBase
{
    /// <summary>
    /// Records a hand count of every cassette. Differences are posted as RECONCILIATION_ADJUSTMENT entries; when their
    /// total TRY value exceeds 1,000 the reconciliation awaits approval and the ATM cannot return to service.
    /// Allowed while CONFIGURED or MAINTENANCE. The X-User header identifies who counted.
    /// </summary>
    [HttpPost("atms/{atmId:guid}/reconciliations")]
    [ProducesResponseType<ReconciliationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReconciliationResponse>> Reconcile(
        Guid atmId, ReconcileRequest request, CancellationToken cancellationToken)
    {
        var reconciliation = await reconciliationService.ReconcileAsync(atmId, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { reconciliationId = reconciliation.ReconciliationId }, reconciliation);
    }

    /// <summary>Returns a reconciliation with its system figures, counts and differences.</summary>
    [HttpGet("reconciliations/{reconciliationId:guid}")]
    [ProducesResponseType<ReconciliationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ReconciliationResponse> GetById(Guid reconciliationId, CancellationToken cancellationToken) =>
        reconciliationService.GetAsync(reconciliationId, cancellationToken);

    /// <summary>
    /// Four-eyes approval: a user other than the one who counted (X-User header) accepts the differences, after which the
    /// ATM may return to service.
    /// </summary>
    [HttpPost("reconciliations/{reconciliationId:guid}/approve")]
    [ProducesResponseType<ReconciliationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ReconciliationResponse> Approve(Guid reconciliationId, CancellationToken cancellationToken) =>
        reconciliationService.ApproveAsync(reconciliationId, cancellationToken);
}
