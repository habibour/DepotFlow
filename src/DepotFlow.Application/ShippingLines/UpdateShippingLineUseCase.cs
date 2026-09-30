using DepotFlow.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.ShippingLines;

public sealed class UpdateShippingLineUseCase(
    IDepotFlowDbContext db,
    IValidator<UpdateShippingLineRequest> validator)
{
    public async Task<Result<ShippingLineDto>> ExecuteAsync(int id, UpdateShippingLineRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var entity = await db.ShippingLines.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ShippingLineErrors.NotFound;
        }

        entity.Update(request.Name!, request.IsActive);   // the code cannot change
        await db.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}
