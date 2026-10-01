using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Reports;

public sealed class YardOccupancyReportUseCase(IDepotFlowDbContext db)
{
    public async Task<YardOccupancyReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        var all = await db.SqlQuery<YardOccupancyRow>($"EXEC dbo.usp_YardOccupancy").ToListAsync(cancellationToken);

        // The procedure returns one row per block followed by a TOTAL row.
        return new YardOccupancyReport(all.Where(r => r.Block != "TOTAL").ToList(), all.Single(r => r.Block == "TOTAL"));
    }
}
