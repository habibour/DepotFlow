using DepotFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Containers;

public sealed class SearchContainersUseCase(IDepotFlowDbContext db)
{
    public async Task<PagedResult<ContainerListItemDto>> ExecuteAsync(
        string? number, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.Containers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(number))
        {
            var prefix = number.Trim().ToUpperInvariant();
            query = query.Where(c => c.Number.StartsWith(prefix));   // becomes LIKE 'PREFIX%' and uses the index
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Number)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(c => new ContainerListItemDto(c.Id, c.Number, c.SizeFeet))
            .ToListAsync(cancellationToken);

        return new PagedResult<ContainerListItemDto>(items, p, size, total);
    }
}
