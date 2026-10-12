using AtmCashManagement.Application.ExchangeRates;
using AtmCashManagement.Domain.Cash;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api/exchange-rates")]
public sealed class ExchangeRatesController(ExchangeRateService exchangeRateService) : ControllerBase
{
    /// <summary>TRY value of each currency, used for reconciliation thresholds and report totals.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExchangeRateResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ExchangeRateResponse>> List(CancellationToken cancellationToken) =>
        exchangeRateService.ListAsync(cancellationToken);

    /// <summary>Updates the TRY rate of a currency (the TRY rate itself is fixed at 1).</summary>
    [HttpPut("{currency}")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExchangeRateResponse> Update(Currency currency, UpdateExchangeRateRequest request, CancellationToken cancellationToken) =>
        exchangeRateService.UpdateAsync(currency, request, cancellationToken);
}
