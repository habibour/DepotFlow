using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Yard;

public sealed class ListYardSlotsUseCase(IDepotFlowDbContext db)
{
    public const int MaxPageSize = 200;

    public async Task<PagedResult<YardSlotDto>> ExecuteAsync(
        string? block, int? row, bool? occupied, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize, MaxPageSize);

        var query = db.YardSlots.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(block))
        {
            var b = block.Trim().ToUpperInvariant();
            query = query.Where(s => s.Block == b);
        }

        if (row is not null)
        {
            query = query.Where(s => s.Row == row);
        }

        if (occupied is not null)
        {
            // A slot is occupied when an in-yard visit points at it; there is no separate flag to keep in sync.
            query = occupied.Value
                ? query.Where(s => db.Visits.Any(v => v.YardSlotId == s.Id && v.Status == VisitStatus.InYard))
                : query.Where(s => !db.Visits.Any(v => v.YardSlotId == s.Id && v.Status == VisitStatus.InYard));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.Block).ThenBy(s => s.Row).ThenBy(s => s.Bay).ThenBy(s => s.Tier)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(s => new YardSlotDto(
                s.Id, s.Code, s.Block, s.Row, s.Bay, s.Tier,
                db.Visits
                    .Where(v => v.YardSlotId == s.Id && v.Status == VisitStatus.InYard)
                    .Select(v => new SlotOccupantDto(v.Id, v.Container.Number, v.Container.SizeFeet))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<YardSlotDto>(items, p, size, total);
    }
}
