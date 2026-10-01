using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Billing;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/visits")]
public class VisitsController : ControllerBase
{
    [HttpPost("gate-in")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.GateClerk}")]
    public async Task<IActionResult> GateIn(
        GateInRequest request, [FromServices] GateInUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(
            await useCase.ExecuteAsync(request, cancellationToken),
            dto => Created($"/api/v1/visits/{dto.Id}", dto));

    [HttpPost("{id:long}/gate-out")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.GateClerk}")]
    public async Task<IActionResult> GateOut(
        long id, GateOutRequest request, [FromServices] GateOutUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, request, cancellationToken), Ok);

    [HttpPost("{id:long}/relocate")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.YardPlanner}")]
    public async Task<IActionResult> Relocate(
        long id, RelocateRequest request, [FromServices] RelocateVisitUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, request, cancellationToken), Ok);

    [HttpGet("{id:long}/charge-preview")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.BillingOfficer}")]
    public async Task<IActionResult> ChargePreview(
        long id, [FromServices] ChargePreviewUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, cancellationToken), Ok);
}
