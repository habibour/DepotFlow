using DepotFlow.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Tariffs;

public sealed class DeactivateTariffUseCase(IDepotFlowDbContext db, IValidator<UpdateTariffRequest> validator)
{
    public async Task<Result<TariffDto>> ExecuteAsync(int id, UpdateTariffRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var tariff = await db.Tariffs.Include(t => t.Tiers).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tariff is null)
        {
            return TariffErrors.NotFound;
        }

        tariff.Deactivate();   // already inactive is fine: the call is idempotent
        await db.SaveChangesAsync(cancellationToken);

        return tariff.ToDto();
    }
}
