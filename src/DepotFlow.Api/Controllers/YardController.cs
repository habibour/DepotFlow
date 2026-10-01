using DepotFlow.Application.Common;
using DepotFlow.Application.Security;
using DepotFlow.Application.Yard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/yard")]
[Authorize(Roles = $"{Roles.Admin},{Roles.GateClerk},{Roles.YardPlanner}")]
public class YardController : ControllerBase
{
    [HttpGet("slots")]
    public async Task<ActionResult<PagedResult<YardSlotDto>>> Slots(
        [FromQuery] string? block, [FromQuery] int? row, [FromQuery] bool? occupied,
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] ListYardSlotsUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(block, row, occupied, page, pageSize, cancellationToken));

    [HttpGet("occupancy")]
    public async Task<ActionResult<IReadOnlyList<BlockOccupancyDto>>> Occupancy(
        [FromServices] GetYardOccupancyUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));
}
