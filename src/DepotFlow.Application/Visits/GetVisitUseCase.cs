using DepotFlow.Application.Common;
using DepotFlow.Application.Gate;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Visits;

public sealed class GetVisitUseCase(IDepotFlowDbContext db, IClock clock)
{
    public async Task<Result<VisitDetailDto>> ExecuteAsync(long id, CancellationToken cancellationToken)
    {
        var found = await db.Visits.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new
            {
                Row = new VisitRow(
                    v.Id, v.ContainerId, v.Container.Number, v.Container.SizeFeet, v.ShippingLine.Code,
                    v.YardSlot == null ? null : v.YardSlot.Code, v.Status, v.GateInAtUtc, v.GateOutAtUtc),
                Invoice = db.Invoices
                    .Where(i => i.VisitId == v.Id)
                    .Select(i => new InvoiceSummaryDto(i.Id, i.InvoiceNumber, i.Total, i.Currency, i.Status))
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return GateErrors.VisitNotFound;
        }

        var r = found.Row;
        return new VisitDetailDto(
            r.Id, r.ContainerId, r.ContainerNumber, r.SizeFeet, r.ShippingLineCode, r.SlotCode, r.Status,
            r.GateInAtUtc, r.GateOutAtUtc, VisitQueryMapping.DwellSoFar(r, clock.UtcNow), found.Invoice);
    }
}
