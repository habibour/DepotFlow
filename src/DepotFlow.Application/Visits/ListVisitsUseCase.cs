using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Visits;

public sealed class ListVisitsUseCase(IDepotFlowDbContext db, IClock clock)
{
    public async Task<PagedResult<VisitListItemDto>> ExecuteAsync(
        VisitStatus? status, int? shippingLineId, string? containerNumber, DateTime? gateInFrom, DateTime? gateInTo,
        string? sort, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.Visits.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(v => v.Status == status);
        }

        if (shippingLineId is not null)
        {
            query = query.Where(v => v.ShippingLineId == shippingLineId);
        }

        if (!string.IsNullOrWhiteSpace(containerNumber))
        {
            var prefix = containerNumber.Trim().ToUpperInvariant();
            query = query.Where(v => v.Container.Number.StartsWith(prefix));
        }

        if (gateInFrom is not null)
        {
            query = query.Where(v => v.GateInAtUtc >= gateInFrom);
        }

        if (gateInTo is not null)
        {
            query = query.Where(v => v.GateInAtUtc <= gateInTo);
        }

        // sort = gateInAtUtc (default, oldest first) or -gateInAtUtc (newest first); Id breaks ties so paging is stable.
        var newestFirst = sort?.Trim() == "-gateInAtUtc";
        var ordered = newestFirst
            ? query.OrderByDescending(v => v.GateInAtUtc).ThenByDescending(v => v.Id)
            : query.OrderBy(v => v.GateInAtUtc).ThenBy(v => v.Id);

        var total = await query.CountAsync(cancellationToken);
        var rows = await ordered
            .Skip((p - 1) * size)
            .Take(size)
            .Select(v => new VisitRow(
                v.Id, v.ContainerId, v.Container.Number, v.Container.SizeFeet, v.ShippingLine.Code,
                v.YardSlot == null ? null : v.YardSlot.Code, v.Status, v.GateInAtUtc, v.GateOutAtUtc))
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var items = rows
            .Select(r => new VisitListItemDto(
                r.Id, r.ContainerId, r.ContainerNumber, r.SizeFeet, r.ShippingLineCode, r.SlotCode, r.Status,
                r.GateInAtUtc, r.GateOutAtUtc, VisitQueryMapping.DwellSoFar(r, now)))
            .ToList();

        return new PagedResult<VisitListItemDto>(items, p, size, total);
    }
}
