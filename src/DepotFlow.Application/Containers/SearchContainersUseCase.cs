using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Containers;

public sealed class SearchContainersUseCase(IDepotFlowDbContext db)
{
    public async Task<PagedResult<ContainerListItemDto>> ExecuteAsync(
        string? number, int? sizeFeet, bool? inYard, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.Containers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(number))
        {
            var prefix = number.Trim().ToUpperInvariant();
            query = query.Where(c => c.Number.StartsWith(prefix));   // becomes LIKE 'PREFIX%' and uses the index
        }

        if (sizeFeet is not null)
        {
            query = query.Where(c => c.SizeFeet == sizeFeet);
        }

        if (inYard is not null)
        {
            // "In the yard" means having an active visit; there is no flag on the container to keep in sync.
            query = inYard.Value
                ? query.Where(c => c.Visits.Any(v => v.Status == VisitStatus.InYard))
                : query.Where(c => !c.Visits.Any(v => v.Status == VisitStatus.InYard));
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
