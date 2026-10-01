using DepotFlow.Application.Audit;
using DepotFlow.Application.Common;
using DepotFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = Roles.Admin)]
public class AuditLogsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] string? entityName, [FromQuery] string? entityKey, [FromQuery] string? userId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] ListAuditLogsUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(entityName, entityKey, userId, from, to, page, pageSize, cancellationToken));
}
