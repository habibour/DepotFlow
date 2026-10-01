using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The four reports as stored procedures. Each exists twice: <c>usp_X_Baseline</c> is the straightforward version
    /// (kept for the benchmark and for correctness tests) and <c>usp_X</c> is the one the API calls. They start
    /// identical; later migrations change only the second.
    ///
    /// Time handling: timestamps are stored in UTC. Bangladesh is UTC+6 all year (no daylight saving), so the
    /// procedures use a fixed +6 hour offset. Date parameters are local (Asia/Dhaka) dates, inclusive at both ends.
    /// </summary>
    public partial class AddReportProcedures : Migration
    {
        private static readonly string[] Names =
        [
            "usp_DailyGateMovements", "usp_YardOccupancy", "usp_AverageDwellTime", "usp_RevenueByShippingLine"
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var suffix in new[] { "_Baseline", "" })
            {
                migrationBuilder.Sql(DailyGateMovements.Replace("__NAME__", "usp_DailyGateMovements" + suffix));
                migrationBuilder.Sql(YardOccupancy.Replace("__NAME__", "usp_YardOccupancy" + suffix));
                migrationBuilder.Sql(AverageDwellTime.Replace("__NAME__", "usp_AverageDwellTime" + suffix));
                migrationBuilder.Sql(RevenueByShippingLine.Replace("__NAME__", "usp_RevenueByShippingLine" + suffix));
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in Names)
            {
                migrationBuilder.Sql($"DROP PROCEDURE [dbo].[{name}]");
                migrationBuilder.Sql($"DROP PROCEDURE [dbo].[{name}_Baseline]");
            }
        }

        // One row per local date in the range (days without activity are zeros, so charts have no holes).
        private const string DailyGateMovements = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date,
                @ShippingLineId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                WITH [Days] AS (
                    SELECT DATEADD(DAY, [value], @FromDate) AS [Date]
                    FROM GENERATE_SERIES(0, DATEDIFF(DAY, @FromDate, @ToDate))
                ),
                [Ins] AS (
                    SELECT CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date) AS [Date], COUNT(*) AS [Count]
                    FROM dbo.Visits v
                    WHERE CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date) BETWEEN @FromDate AND @ToDate
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                    GROUP BY CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date)
                ),
                [Outs] AS (
                    SELECT CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date) AS [Date], COUNT(*) AS [Count]
                    FROM dbo.Visits v
                    WHERE v.GateOutAtUtc IS NOT NULL
                      AND CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date) BETWEEN @FromDate AND @ToDate
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                    GROUP BY CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date)
                )
                SELECT d.[Date], ISNULL(i.[Count], 0) AS GateIns, ISNULL(o.[Count], 0) AS GateOuts
                FROM [Days] d
                LEFT JOIN [Ins] i ON i.[Date] = d.[Date]
                LEFT JOIN [Outs] o ON o.[Date] = d.[Date]
                ORDER BY d.[Date];
            END
            """;

        // One row per block plus a TOTAL row. TEU: a 20 ft container is 1, a 40 ft container is 2.
        private const string YardOccupancy = """
            CREATE PROCEDURE [dbo].[__NAME__]
            AS
            BEGIN
                SET NOCOUNT ON;
                WITH [Slots] AS (
                    SELECT s.Block, COUNT(*) AS TotalSlots FROM dbo.YardSlots s GROUP BY s.Block
                ),
                [Occupied] AS (
                    SELECT s.Block,
                           COUNT(*) AS OccupiedSlots,
                           SUM(CASE WHEN c.SizeFeet = 40 THEN 2 ELSE 1 END) AS OccupiedTeu
                    FROM dbo.Visits v
                    JOIN dbo.YardSlots s ON s.Id = v.YardSlotId
                    JOIN dbo.Containers c ON c.Id = v.ContainerId
                    WHERE v.Status = 1
                    GROUP BY s.Block
                ),
                [PerBlock] AS (
                    SELECT sl.Block, sl.TotalSlots,
                           ISNULL(o.OccupiedSlots, 0) AS OccupiedSlots,
                           ISNULL(o.OccupiedTeu, 0) AS OccupiedTeu
                    FROM [Slots] sl
                    LEFT JOIN [Occupied] o ON o.Block = sl.Block
                )
                SELECT [Block], [TotalSlots], [OccupiedSlots], [OccupiedTeu], [PercentOccupied]
                FROM (
                    SELECT CAST(Block AS varchar(5)) AS [Block], TotalSlots, OccupiedSlots, OccupiedTeu,
                           CAST(ROUND(100.0 * OccupiedSlots / NULLIF(TotalSlots, 0), 1) AS decimal(5, 1)) AS [PercentOccupied]
                    FROM [PerBlock]
                    UNION ALL
                    SELECT 'TOTAL', SUM(TotalSlots), SUM(OccupiedSlots), SUM(OccupiedTeu),
                           CAST(ROUND(100.0 * SUM(OccupiedSlots) / NULLIF(SUM(TotalSlots), 0), 1) AS decimal(5, 1))
                    FROM [PerBlock]
                ) AS r
                ORDER BY CASE WHEN [Block] = 'TOTAL' THEN 1 ELSE 0 END, [Block];
            END
            """;

        // Released visits whose gate-out falls in the range. Dwell days = local calendar dates between gate-in and gate-out, plus one.
        private const string AverageDwellTime = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date,
                @ShippingLineId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                WITH [Released] AS (
                    SELECT v.ShippingLineId,
                           DATEDIFF(DAY, CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date),
                                         CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date)) + 1 AS DwellDays
                    FROM dbo.Visits v
                    WHERE v.Status = 2
                      AND CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date) BETWEEN @FromDate AND @ToDate
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
                ORDER BY AvgDwellDays DESC, l.Code;
            END
            """;

        // Invoices issued in the range (by local date of IssuedAtUtc); only lines that have invoices appear, then a TOTAL row.
        private const string RevenueByShippingLine = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                WITH [PerLine] AS (
                    SELECT i.ShippingLineId,
                           COUNT(*) AS Invoices,
                           SUM(i.Total) AS TotalBilled,
                           SUM(CASE WHEN i.Status = 2 THEN i.Total ELSE CAST(0 AS decimal(18, 2)) END) AS TotalPaid
                    FROM dbo.Invoices i
                    WHERE CAST(DATEADD(HOUR, 6, i.IssuedAtUtc) AS date) BETWEEN @FromDate AND @ToDate
                    GROUP BY i.ShippingLineId
                )
                SELECT [ShippingLineId], [Code], [Invoices], [TotalBilled], [TotalPaid], [Outstanding]
                FROM (
                    SELECT p.ShippingLineId, l.Code, p.Invoices, p.TotalBilled, p.TotalPaid,
                           p.TotalBilled - p.TotalPaid AS Outstanding
                    FROM [PerLine] p
                    JOIN dbo.ShippingLines l ON l.Id = p.ShippingLineId
                    UNION ALL
                    SELECT NULL, N'TOTAL', ISNULL(SUM(Invoices), 0), ISNULL(SUM(TotalBilled), 0),
                           ISNULL(SUM(TotalPaid), 0), ISNULL(SUM(TotalBilled - TotalPaid), 0)
                    FROM [PerLine]
                ) AS r
                ORDER BY CASE WHEN [ShippingLineId] IS NULL THEN 1 ELSE 0 END, [Code];
            END
            """;
    }
}
