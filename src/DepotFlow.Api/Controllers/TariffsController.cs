using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Security;
using DepotFlow.Application.Tariffs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = $"{Roles.Admin},{Roles.BillingOfficer}")]
public class TariffsController : ControllerBase
{
    [HttpGet("shipping-lines/{shippingLineId:int}/tariffs")]
    public async Task<IActionResult> List(
        int shippingLineId, [FromServices] ListTariffsUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(shippingLineId, cancellationToken), Ok);

    [HttpPost("shipping-lines/{shippingLineId:int}/tariffs")]
    public async Task<IActionResult> Create(
        int shippingLineId, CreateTariffRequest request,
        [FromServices] CreateTariffUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(
            await useCase.ExecuteAsync(shippingLineId, request, cancellationToken),
            dto => Created($"/api/v1/shipping-lines/{shippingLineId}/tariffs", dto));

    [HttpPut("tariffs/{id:int}")]
    public async Task<IActionResult> Update(
        int id, UpdateTariffRequest request,
        [FromServices] DeactivateTariffUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, request, cancellationToken), Ok);
}
