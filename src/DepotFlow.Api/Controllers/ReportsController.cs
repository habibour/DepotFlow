using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Reports;
using DepotFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

/// <summary>Reports, each backed by a SQL Server stored procedure. Dates are local (Asia/Dhaka) dates, yyyy-MM-dd, inclusive.</summary>
[ApiController]
[Route("api/v1/reports")]
public class ReportsController : ControllerBase
{
    [HttpGet("daily-movements")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.GateClerk},{Roles.YardPlanner}")]
    public async Task<IActionResult> DailyMovements(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? shippingLineId,
        [FromServices] DailyMovementsReportUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(
            await useCase.ExecuteAsync(new DailyMovementsRequest(from, to, shippingLineId), cancellationToken), Ok);

    [HttpGet("yard-occupancy")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.GateClerk},{Roles.YardPlanner}")]
    public async Task<ActionResult<YardOccupancyReport>> YardOccupancy(
        [FromServices] YardOccupancyReportUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    [HttpGet("dwell-time")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.YardPlanner},{Roles.BillingOfficer}")]
    public async Task<IActionResult> DwellTime(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? shippingLineId,
        [FromServices] DwellTimeReportUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(
            await useCase.ExecuteAsync(new DwellTimeRequest(from, to, shippingLineId), cancellationToken), Ok);

    [HttpGet("revenue")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.BillingOfficer}")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromServices] RevenueReportUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(new RevenueRequest(from, to), cancellationToken), Ok);
}
