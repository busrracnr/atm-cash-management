using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Application.Idempotency;
using AtmCashManagement.Application.Withdrawals;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class WithdrawalsController(WithdrawalService withdrawalService) : ControllerBase
{
    /// <summary>
    /// Step 1 of a withdrawal: returns the fewest-notes breakdown, or the nearest lower/upper amounts when the request
    /// cannot be paid exactly, and reserves the offered notes for 60 seconds. Allowed while the ATM is IN_SERVICE.
    /// </summary>
    [HttpPost("atms/{atmId:guid}/withdrawal-quotes")]
    [ProducesResponseType<WithdrawalQuoteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WithdrawalQuoteResponse>> CreateQuote(
        Guid atmId, CreateWithdrawalQuoteRequest request, CancellationToken cancellationToken)
    {
        var quote = await withdrawalService.CreateQuoteAsync(atmId, request, cancellationToken);

        return CreatedAtAction(nameof(GetQuote), new { quoteId = quote.QuoteId }, quote);
    }

    /// <summary>Returns a withdrawal quote and its current status.</summary>
    [HttpGet("withdrawal-quotes/{quoteId:guid}")]
    [ProducesResponseType<WithdrawalQuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<WithdrawalQuoteResponse> GetQuote(Guid quoteId, CancellationToken cancellationToken) =>
        withdrawalService.GetQuoteAsync(quoteId, cancellationToken);

    /// <summary>
    /// Step 2 of a withdrawal: pays out the chosen option and releases the rest of the reservation.
    /// Repeating the request with the same Idempotency-Key returns the original result and never pays twice.
    /// </summary>
    /// <param name="quoteId">Quote to confirm.</param>
    /// <param name="idempotencyKey">Unique value per confirmation attempt (e.g. a GUID); reuse it only to retry the same request.</param>
    /// <param name="request">Option to take; may be omitted when the amount can be paid exactly.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpPost("withdrawal-quotes/{quoteId:guid}/confirm")]
    [ProducesResponseType<WithdrawalConfirmationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<WithdrawalConfirmationResponse> Confirm(
        Guid quoteId,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(IdempotencyRecord.KeyMaxLength)] string idempotencyKey,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ConfirmWithdrawalQuoteRequest? request,
        CancellationToken cancellationToken) =>
        withdrawalService.ConfirmAsync(quoteId, idempotencyKey, request ?? new ConfirmWithdrawalQuoteRequest(null), cancellationToken);

    /// <summary>Cancels an open quote and releases its notes. Cancelling an already cancelled quote is a no-op.</summary>
    [HttpPost("withdrawal-quotes/{quoteId:guid}/cancel")]
    [ProducesResponseType<WithdrawalQuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<WithdrawalQuoteResponse> Cancel(Guid quoteId, CancellationToken cancellationToken) =>
        withdrawalService.CancelAsync(quoteId, cancellationToken);
}
