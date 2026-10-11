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

    /// <summary>Returns a single ATM.</summary>
    [HttpGet("{atmId:guid}")]
    [ProducesResponseType<AtmResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AtmResponse> GetById(Guid atmId, CancellationToken cancellationToken) =>
        atmService.GetAsync(atmId, cancellationToken);

    /// <summary>Lists all ATMs ordered by code.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AtmResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AtmResponse>> List(CancellationToken cancellationToken) =>
        atmService.ListAsync(cancellationToken);
}
