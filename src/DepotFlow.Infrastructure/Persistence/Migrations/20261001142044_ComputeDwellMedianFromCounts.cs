using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Problem: PERCENTILE_CONT needs every visit sorted per shipping line, which dominated the 36 month dwell report
    /// even after the index work (median 299 ms with only 6,447 logical reads). Change: dwell days are small whole
    /// numbers, so count visits per (line, dwell days) and take the median from the cumulative counts.
    /// The result must equal PERCENTILE_CONT exactly; the benchmark compares row hashes and REP-5/REP-7 cover it.
    /// </summary>
    public partial class ComputeDwellMedianFromCounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CurrentAverageDwellTime.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_AverageDwellTime"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreviousAverageDwellTime.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_AverageDwellTime"));
        }

        private const string PreviousAverageDwellTime = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date,
                @ShippingLineId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                DECLARE @FromUtc datetime2 = DATEADD(HOUR, -6, CAST(@FromDate AS datetime2));
                DECLARE @ToUtc datetime2 = DATEADD(HOUR, -6, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2));  -- exclusive

                WITH [Released] AS (
                    SELECT v.ShippingLineId,
                           DATEDIFF(DAY, CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date),
                                         CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date)) + 1 AS DwellDays
                    FROM dbo.Visits v
                    WHERE v.Status = 2
                      AND v.GateOutAtUtc >= @FromUtc AND v.GateOutAtUtc < @ToUtc
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                ),
                [Ranked] AS (
                    -- PERCENTILE_CONT is a window function: it repeats the median on every row of its partition.
                    SELECT ShippingLineId, DwellDays,
                           PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY DwellDays) OVER (PARTITION BY ShippingLineId) AS MedianDwell
                    FROM [Released]
                )
                SELECT r.ShippingLineId, l.Code,
                       COUNT(*) AS ReleasedVisits,
                       CAST(ROUND(AVG(CAST(r.DwellDays AS decimal(18, 4))), 1) AS decimal(9, 1)) AS AvgDwellDays,
                       CAST(MAX(r.MedianDwell) AS decimal(9, 1)) AS MedianDwellDays,
                       MAX(r.DwellDays) AS MaxDwellDays
                FROM [Ranked] r
                JOIN dbo.ShippingLines l ON l.Id = r.ShippingLineId
                GROUP BY r.ShippingLineId, l.Code
                ORDER BY AvgDwellDays DESC, l.Code
                -- Compile with the real dates: the bounds sit in local variables, which the optimizer cannot see
                -- when it caches a plan, so it guessed 16.4% of the table for any range and chose a serial plan.
                OPTION (RECOMPILE);
            END
            """;

        private const string CurrentAverageDwellTime = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date,
                @ShippingLineId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                DECLARE @FromUtc datetime2 = DATEADD(HOUR, -6, CAST(@FromDate AS datetime2));
                DECLARE @ToUtc datetime2 = DATEADD(HOUR, -6, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2));  -- exclusive

                -- Dwell days are small whole numbers (1 to about 60). Instead of sorting every visit for PERCENTILE_CONT,
                -- count the visits per (line, dwell days) first, then read the median off the cumulative counts.
                -- PERCENTILE_CONT(0.5) is the mean of the values at ranks (N + 1) / 2 and N / 2 + 1 (integer division):
                -- the same rank for an odd N, the two middle ranks for an even N.
                WITH [Counts] AS (
                    SELECT v.ShippingLineId,
                           DATEDIFF(DAY, CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date),
                                         CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date)) + 1 AS DwellDays,
                           COUNT(*) AS Cnt
                    FROM dbo.Visits v
                    WHERE v.Status = 2
                      AND v.GateOutAtUtc >= @FromUtc AND v.GateOutAtUtc < @ToUtc
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                    GROUP BY v.ShippingLineId,
                             DATEDIFF(DAY, CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date),
                                           CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date)) + 1
                ),
                [Cumulative] AS (
                    SELECT ShippingLineId, DwellDays, Cnt,
                           SUM(Cnt) OVER (PARTITION BY ShippingLineId ORDER BY DwellDays ROWS UNBOUNDED PRECEDING) AS CumCnt,
                           SUM(Cnt) OVER (PARTITION BY ShippingLineId) AS Total
                    FROM [Counts]
                )
                SELECT c.ShippingLineId, l.Code,
                       SUM(c.Cnt) AS ReleasedVisits,
                       CAST(ROUND(SUM(CAST(c.DwellDays AS decimal(18, 4)) * c.Cnt) / SUM(c.Cnt), 1) AS decimal(9, 1)) AS AvgDwellDays,
                       CAST((MIN(CASE WHEN c.CumCnt >= (c.Total + 1) / 2 THEN c.DwellDays END)
                           + MIN(CASE WHEN c.CumCnt >= c.Total / 2 + 1 THEN c.DwellDays END)) / 2.0 AS decimal(9, 1)) AS MedianDwellDays,
                       MAX(c.DwellDays) AS MaxDwellDays
                FROM [Cumulative] c
                JOIN dbo.ShippingLines l ON l.Id = c.ShippingLineId
                GROUP BY c.ShippingLineId, l.Code
                ORDER BY AvgDwellDays DESC, l.Code
                -- Compile with the real dates (see RecompileReportStatements).
                OPTION (RECOMPILE);
            END
            """;
    }
}
