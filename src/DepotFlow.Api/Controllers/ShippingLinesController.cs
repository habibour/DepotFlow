using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Common;
using DepotFlow.Application.Security;
using DepotFlow.Application.ShippingLines;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/shipping-lines")]
public class ShippingLinesController : ControllerBase
{
    // Any authenticated user may read (the default-deny policy already requires sign-in).
    [HttpGet]
    public async Task<ActionResult<PagedResult<ShippingLineDto>>> List(
        [FromQuery] bool? isActive, [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] ListShippingLinesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(isActive, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id, [FromServices] GetShippingLineUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, cancellationToken), Ok);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(
        CreateShippingLineRequest request,
        [FromServices] CreateShippingLineUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(
            await useCase.ExecuteAsync(request, cancellationToken),
            dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto));

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(
        int id, UpdateShippingLineRequest request,
        [FromServices] UpdateShippingLineUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, request, cancellationToken), Ok);
}
