using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.ShippingLines;

public sealed class CreateShippingLineUseCase(
    IDepotFlowDbContext db,
    IValidator<CreateShippingLineRequest> validator,
    IClock clock)
{
    public async Task<Result<ShippingLineDto>> ExecuteAsync(CreateShippingLineRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var entity = new ShippingLine(request.Code!, request.Name!, clock.UtcNow);

        // Friendly check first; the unique index on Code is the real guard if two requests race.
        if (await db.ShippingLines.AnyAsync(x => x.Code == entity.Code, cancellationToken))
        {
            return ShippingLineErrors.CodeExists;
        }

        db.ShippingLines.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return ShippingLineErrors.CodeExists;
        }

        return entity.ToDto();
    }
}
