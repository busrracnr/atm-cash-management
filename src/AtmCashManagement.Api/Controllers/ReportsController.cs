using AtmCashManagement.Application.Reports;

using Microsoft.AspNetCore.Mvc;

namespace AtmCashManagement.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ReportsController(ReportService reportService) : ControllerBase
{
    /// <summary>
    /// Operations report: per ATM and cassette the note, reserved and available counts, totals per currency and the overall
    /// total in TRY.
    /// </summary>
    /// <param name="atmId">Optional: report a single ATM instead of the whole network.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("reports/operations")]
    [ProducesResponseType<OperationsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OperationsReportResponse> GetOperationsReport([FromQuery] Guid? atmId, CancellationToken cancellationToken) =>
        reportService.GetOperationsReportAsync(atmId, cancellationToken);

    /// <summary>Low-cash alerts written to the outbox, newest first; processedAt is set once the dispatcher published them.</summary>
    /// <param name="atmId">Optional ATM filter.</param>
    /// <param name="take">Maximum number of alerts, 1 to 500.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("alerts")]
    [ProducesResponseType<IReadOnlyList<AlertResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AlertResponse>> GetAlerts(
        [FromQuery] Guid? atmId, [FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        reportService.GetAlertsAsync(atmId, take, cancellationToken);
}
