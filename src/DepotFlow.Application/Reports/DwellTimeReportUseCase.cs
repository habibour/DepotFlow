using DepotFlow.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Reports;

public sealed class DwellTimeReportUseCase(IDepotFlowDbContext db, IValidator<DwellTimeRequest> validator)
{
    public async Task<Result<DwellTimeReport>> ExecuteAsync(DwellTimeRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var from = request.From!.Value;
        var to = request.To!.Value;
        var line = request.ShippingLineId;

        var rows = await db.SqlQuery<DwellTimeRow>(
                $"EXEC dbo.usp_AverageDwellTime @FromDate = {from}, @ToDate = {to}, @ShippingLineId = {line}")
            .ToListAsync(cancellationToken);

        return new DwellTimeReport(from, to, line, rows);
    }
}
