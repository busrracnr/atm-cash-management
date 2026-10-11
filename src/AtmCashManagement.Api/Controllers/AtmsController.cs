using AtmCashManagement.Application.Atms;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api/atms")]
public sealed class AtmsController(AtmService atmService) : ControllerBase
{
    /// <summary>Registers a new ATM. A new ATM starts in the UNCONFIGURED state.</summary>
    [HttpPost]
    [ProducesResponseType<AtmResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AtmResponse>> Create(CreateAtmRequest request, CancellationToken cancellationToken)
    {
        var atm = await atmService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { atmId = atm.Id }, atm);
    }

    /// <summary>Returns an ATM with its cassettes and the notes in each.</summary>
    [HttpGet("{atmId:guid}")]
    [ProducesResponseType<AtmDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AtmDetailsResponse> GetById(Guid atmId, CancellationToken cancellationToken) =>
        atmService.GetAsync(atmId, cancellationToken);

    /// <summary>Lists all ATMs ordered by code.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AtmResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AtmResponse>> List(CancellationToken cancellationToken) =>
        atmService.ListAsync(cancellationToken);

    /// <summary>
    /// Configures all five cassettes: one DEPOSIT and four RECYCLING, of which one or two hold USD/EUR.
    /// Allowed while UNCONFIGURED (moves to CONFIGURED), CONFIGURED or MAINTENANCE. A cassette can only be redefined when empty.
    /// </summary>
    [HttpPut("{atmId:guid}/cassettes")]
    [ProducesResponseType<AtmDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<AtmDetailsResponse> ConfigureCassettes(
        Guid atmId, ConfigureCassettesRequest request, CancellationToken cancellationToken) =>
        atmService.ConfigureCassettesAsync(atmId, request, cancellationToken);

    /// <summary>
    /// Changes the ATM status. LOADED → IN_SERVICE, IN_SERVICE → MAINTENANCE and MAINTENANCE → IN_SERVICE can be requested;
    /// any other transition is rejected with INVALID_STATE_TRANSITION.
    /// </summary>
    [HttpPut("{atmId:guid}/status")]
    [ProducesResponseType<AtmDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AtmDetailsResponse> ChangeStatus(
        Guid atmId, ChangeAtmStatusRequest request, CancellationToken cancellationToken) =>
        atmService.ChangeStatusAsync(atmId, request, cancellationToken);
}
