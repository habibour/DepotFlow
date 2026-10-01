using DepotFlow.Application.Common;
using DepotFlow.Application.Tariffs;
using DepotFlow.Domain;
using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Billing;

public sealed record ChargeQuote(TariffSnapshot Tariff, int DwellDays, ChargeResult Charge);

/// <summary>
/// The one place that turns "this visit, leaving at this moment" into a charge. Gate-out and the charge preview
/// both call it, so a preview can never disagree with the invoice that follows.
/// </summary>
public sealed class ChargeCalculator(IDepotFlowDbContext db)
{
    /// <param name="visit">Must have its <see cref="Visit.Container"/> loaded (the size picks the tariff).</param>
    public async Task<Result<ChargeQuote>> QuoteAsync(Visit visit, DateTime atUtc, CancellationToken cancellationToken)
    {
        var tariff = await db.Tariffs.AsNoTracking()
            .Include(t => t.Tiers)
            .FirstOrDefaultAsync(
                t => t.ShippingLineId == visit.ShippingLineId && t.SizeFeet == visit.Container.SizeFeet && t.IsActive,
                cancellationToken);
        if (tariff is null)
        {
            return TariffErrors.NoActiveTariff;
        }

        var snapshot = tariff.ToSnapshot();
        var dwellDays = DwellCalculator.Days(visit.GateInAtUtc, atUtc, DepotTimeZone.Dhaka);
        var charge = TariffStrategyFactory.For(snapshot.StrategyKey).Calculate(snapshot, dwellDays);

        return new ChargeQuote(snapshot, dwellDays, charge);
    }
}
