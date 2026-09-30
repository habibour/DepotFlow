using DepotFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.ShippingLines;

public sealed class GetShippingLineUseCase(IDepotFlowDbContext db)
{
    public async Task<Result<ShippingLineDto>> ExecuteAsync(int id, CancellationToken cancellationToken)
    {
        var dto = await db.ShippingLines.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ShippingLineMapping.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null ? ShippingLineErrors.NotFound : dto;
    }
}
