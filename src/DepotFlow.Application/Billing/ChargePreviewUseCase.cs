using DepotFlow.Application.Common;
using DepotFlow.Application.Gate;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Billing;

/// <summary>What the charge would be if the container left right now. Changes nothing.</summary>
public sealed class ChargePreviewUseCase(IDepotFlowDbContext db, ChargeCalculator chargeCalculator, IClock clock)
{
    public async Task<Result<ChargePreviewDto>> ExecuteAsync(long visitId, CancellationToken cancellationToken)
    {
        var visit = await db.Visits.AsNoTracking()
            .Include(v => v.Container)
            .FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken);
        if (visit is null)
        {
            return GateErrors.VisitNotFound;
        }

        if (visit.Status != VisitStatus.InYard)
        {
            return GateErrors.VisitNotInYard;
        }

        var quote = await chargeCalculator.QuoteAsync(visit, clock.UtcNow, cancellationToken);
        if (!quote.IsSuccess)
        {
            return quote.Error;
        }

        var q = quote.Value;
        return new ChargePreviewDto(
            visit.Id, q.DwellDays, q.Tariff.FreeDays, q.Tariff.StrategyKey, q.Tariff.Currency,
            q.Charge.Lines.Select(l => new ChargeLineDto(l.FromDay, l.ToDay, l.Days, l.RatePerDay, l.Amount)).ToList(),
            q.Charge.Total);
    }
}
