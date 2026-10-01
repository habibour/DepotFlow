using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Common;
using DepotFlow.Application.Containers;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

// Any authenticated user may read containers (the default-deny policy already requires sign-in).
[ApiController]
[Route("api/v1/containers")]
public class ContainersController : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id, [FromServices] GetContainerUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, cancellationToken), Ok);

    [HttpGet]
    public async Task<ActionResult<PagedResult<ContainerListItemDto>>> Search(
        [FromQuery] string? number, [FromQuery] int? sizeFeet, [FromQuery] bool? inYard, [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] SearchContainersUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(number, sizeFeet, inYard, page, pageSize, cancellationToken));
}
