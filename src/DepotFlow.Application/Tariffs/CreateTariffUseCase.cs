using DepotFlow.Application.Common;
using DepotFlow.Application.ShippingLines;
using DepotFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Tariffs;

public sealed class CreateTariffUseCase(
    IDepotFlowDbContext db,
    IValidator<CreateTariffRequest> validator,
    IClock clock)
{
    public async Task<Result<TariffDto>> ExecuteAsync(
        int shippingLineId, CreateTariffRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        if (!await db.ShippingLines.AnyAsync(x => x.Id == shippingLineId, cancellationToken))
        {
            return ShippingLineErrors.NotFound;
        }

        var tiers = request.Tiers!.Select(t => new TariffTier(t.FromDay!.Value, t.ToDay, t.RatePerDay!.Value));
        var tariff = new Tariff(
            shippingLineId, request.SizeFeet!.Value, request.StrategyKey!, request.FreeDays!.Value, tiers, clock.UtcNow);

        if (tariff.Validate() is { } rule)
        {
            return TariffErrors.Invalid(rule);
        }

        // Two saves in one transaction. The unique index allows only one active tariff per line and size,
        // so the old one must be switched off before the new one is inserted; if the insert fails, the
        // switch-off is rolled back too and the old tariff stays active.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var previous = await db.Tariffs.FirstOrDefaultAsync(
            t => t.ShippingLineId == shippingLineId && t.SizeFeet == tariff.SizeFeet && t.IsActive, cancellationToken);
        if (previous is not null)
        {
            previous.Deactivate();
            await db.SaveChangesAsync(cancellationToken);
        }

        db.Tariffs.Add(tariff);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return TariffErrors.Conflict;   // a concurrent create won; leaving the scope rolls everything back
        }

        await transaction.CommitAsync(cancellationToken);
        return tariff.ToDto();
    }
}
