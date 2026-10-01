using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application.Common;
using DepotFlow.Application.Invoices;
using DepotFlow.Application.Security;
using DepotFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize(Roles = $"{Roles.Admin},{Roles.BillingOfficer}")]
public class InvoicesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> List(
        [FromQuery] InvoiceStatus? status, [FromQuery] int? shippingLineId,
        [FromQuery] DateTime? issuedFrom, [FromQuery] DateTime? issuedTo, [FromQuery] string? invoiceNumber,
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] ListInvoicesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(status, shippingLineId, issuedFrom, issuedTo, invoiceNumber, page, pageSize, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(
        long id, [FromServices] GetInvoiceUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, cancellationToken), Ok);

    [HttpPost("{id:long}/pay")]
    public async Task<IActionResult> Pay(
        long id, [FromServices] PayInvoiceUseCase useCase, CancellationToken cancellationToken) =>
        this.ToActionResult(await useCase.ExecuteAsync(id, cancellationToken), Ok);
}
