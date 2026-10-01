using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Yard;

public sealed class GetYardOccupancyUseCase(IDepotFlowDbContext db)
{
    public async Task<IReadOnlyList<BlockOccupancyDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Two simple grouped queries, merged here, instead of one correlated query per slot.
        var totals = await db.YardSlots.AsNoTracking()
            .GroupBy(s => s.Block)
            .Select(g => new { Block = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var occupied = await db.Visits.AsNoTracking()
            .Where(v => v.Status == VisitStatus.InYard && v.YardSlotId != null)
            .GroupBy(v => v.YardSlot!.Block)
            .Select(g => new { Block = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Block, x => x.Count, cancellationToken);

        return totals
            .OrderBy(t => t.Block)
            .Select(t =>
            {
                var used = occupied.GetValueOrDefault(t.Block);
                return new BlockOccupancyDto(t.Block, t.Count, used, Math.Round(100m * used / t.Count, 1));
            })
            .ToList();
    }
}
