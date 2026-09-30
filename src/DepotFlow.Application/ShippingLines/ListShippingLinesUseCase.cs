using DepotFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.ShippingLines;

public sealed class ListShippingLinesUseCase(IDepotFlowDbContext db)
{
    public async Task<PagedResult<ShippingLineDto>> ExecuteAsync(
        bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.ShippingLines.AsNoTracking();
        if (isActive is not null)
        {
            query = query.Where(x => x.IsActive == isActive);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(ShippingLineMapping.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<ShippingLineDto>(items, p, size, total);
    }
}
