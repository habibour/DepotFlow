using DepotFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Containers;

public sealed class GetContainerUseCase(IDepotFlowDbContext db)
{
    private static readonly Error NotFound = Error.NotFound("container_not_found", "Container was not found.");

    public async Task<Result<ContainerDetailDto>> ExecuteAsync(int id, CancellationToken cancellationToken)
    {
        // One projection: EF selects only these columns, with the visits newest first.
        var dto = await db.Containers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ContainerDetailDto(
                c.Id,
                c.Number,
                c.SizeFeet,
                c.Visits
                    .OrderByDescending(v => v.GateInAtUtc)
                    .Select(v => new ContainerVisitDto(v.Id, v.Status, v.ShippingLine.Code, v.GateInAtUtc, v.GateOutAtUtc))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null ? NotFound : dto;
    }
}
