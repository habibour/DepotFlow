using DepotFlow.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Reports;

public sealed class RevenueReportUseCase(IDepotFlowDbContext db, IValidator<RevenueRequest> validator)
{
    // All tariffs and invoices are in Bangladeshi taka.
    private const string Currency = "BDT";

    public async Task<Result<RevenueReport>> ExecuteAsync(RevenueRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var from = request.From!.Value;
        var to = request.To!.Value;

        var all = await db.SqlQuery<RevenueRow>($"EXEC dbo.usp_RevenueByShippingLine @FromDate = {from}, @ToDate = {to}")
            .ToListAsync(cancellationToken);

        // One row per shipping line that has invoices, then a TOTAL row (ShippingLineId is null on it).
        var rows = all.Where(r => r.ShippingLineId is not null).ToList();
        var total = all.Single(r => r.ShippingLineId is null);

        return new RevenueReport(
            from, to, Currency, rows, new RevenueTotal(total.Invoices, total.TotalBilled, total.TotalPaid, total.Outstanding));
    }
}
