using DepotFlow.Application.Common;
using DepotFlow.Application.ShippingLines;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Tariffs;

public sealed class ListTariffsUseCase(IDepotFlowDbContext db)
{
    public async Task<Result<IReadOnlyList<TariffDto>>> ExecuteAsync(int shippingLineId, CancellationToken cancellationToken)
    {
        if (!await db.ShippingLines.AnyAsync(x => x.Id == shippingLineId, cancellationToken))
        {
            return ShippingLineErrors.NotFound;
        }

        IReadOnlyList<TariffDto> tariffs = await db.Tariffs.AsNoTracking()
            .Where(t => t.ShippingLineId == shippingLineId)
            .OrderBy(t => t.SizeFeet).ThenByDescending(t => t.IsActive).ThenByDescending(t => t.CreatedAtUtc)
            .Select(TariffMapping.Projection)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<TariffDto>>.Success(tariffs);
    }
}
