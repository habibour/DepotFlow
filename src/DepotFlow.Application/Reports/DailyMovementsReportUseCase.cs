using DepotFlow.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Reports;

public sealed class DailyMovementsReportUseCase(IDepotFlowDbContext db, IValidator<DailyMovementsRequest> validator)
{
    public async Task<Result<DailyMovementsReport>> ExecuteAsync(DailyMovementsRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var from = request.From!.Value;
        var to = request.To!.Value;
        var line = request.ShippingLineId;

        var rows = await db.SqlQuery<DailyMovementRow>(
                $"EXEC dbo.usp_DailyGateMovements @FromDate = {from}, @ToDate = {to}, @ShippingLineId = {line}")
            .ToListAsync(cancellationToken);

        return new DailyMovementsReport(
            from, to, line, rows, new GateMovementTotal(rows.Sum(r => r.GateIns), rows.Sum(r => r.GateOuts)));
    }
}
