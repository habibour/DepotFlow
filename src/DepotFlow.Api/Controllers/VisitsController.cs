using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/visits")]
[Authorize(Roles = $"{Roles.Admin},{Roles.GateClerk}")]
public class VisitsController : ControllerBase
{
    [HttpPost("gate-in")]
    public async Task<IActionResult> GateIn(
        GateInRequest request, [FromServices] GateInUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(
            await useCase.ExecuteAsync(request, cancellationToken),
            dto => Created($"/api/v1/visits/{dto.Id}", dto));

    [HttpPost("{id:long}/gate-out")]
    public async Task<IActionResult> GateOut(
        long id, GateOutRequest request, [FromServices] GateOutUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, request, cancellationToken), Ok);
}
