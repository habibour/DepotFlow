using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Problem: the UTC bounds are held in local variables, so the optimizer cannot use their values when it compiles
    /// the statement. It guesses a fixed 16.4% of the table for the range (246,395 of 1,499,510 invoices), which for a
    /// wide range yields a cheap-looking serial plan (cost 3.38 is below the parallelism threshold of 5) that is slower
    /// than the parallel scan it replaced. Change: OPTION (RECOMPILE) compiles each call with the actual values.
    /// Cost: a few milliseconds of compile time per call and no plan reuse, which is fine for reports.
    /// </summary>
    public partial class RecompileReportStatements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CurrentDailyGateMovements.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_DailyGateMovements"));
            migrationBuilder.Sql(CurrentAverageDwellTime.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_AverageDwellTime"));
            migrationBuilder.Sql(CurrentRevenueByShippingLine.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_RevenueByShippingLine"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreviousDailyGateMovements.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_DailyGateMovements"));
            migrationBuilder.Sql(PreviousAverageDwellTime.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_AverageDwellTime"));
            migrationBuilder.Sql(PreviousRevenueByShippingLine.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_RevenueByShippingLine"));
        }

        private const string PreviousDailyGateMovements = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date,
                @ShippingLineId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                -- Convert the range to UTC bounds once, so the filters compare the raw column (sargable)
                -- instead of wrapping every row's timestamp in functions.
                DECLARE @FromUtc datetime2 = DATEADD(HOUR, -6, CAST(@FromDate AS datetime2));
                DECLARE @ToUtc datetime2 = DATEADD(HOUR, -6, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2));  -- exclusive

                WITH [Days] AS (
                    SELECT DATEADD(DAY, [value], @FromDate) AS [Date]
                    FROM GENERATE_SERIES(0, DATEDIFF(DAY, @FromDate, @ToDate))
                ),
                [Ins] AS (
                    SELECT CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date) AS [Date], COUNT(*) AS [Count]
                    FROM dbo.Visits v
                    WHERE v.GateInAtUtc >= @FromUtc AND v.GateInAtUtc < @ToUtc
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                    GROUP BY CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date)
                ),
                [Outs] AS (
                    SELECT CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date) AS [Date], COUNT(*) AS [Count]
                    FROM dbo.Visits v
                    WHERE v.GateOutAtUtc >= @FromUtc AND v.GateOutAtUtc < @ToUtc
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

        private const string CurrentDailyGateMovements = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date,
                @ShippingLineId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                -- Convert the range to UTC bounds once, so the filters compare the raw column (sargable)
                -- instead of wrapping every row's timestamp in functions.
                DECLARE @FromUtc datetime2 = DATEADD(HOUR, -6, CAST(@FromDate AS datetime2));
                DECLARE @ToUtc datetime2 = DATEADD(HOUR, -6, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2));  -- exclusive

                WITH [Days] AS (
                    SELECT DATEADD(DAY, [value], @FromDate) AS [Date]
                    FROM GENERATE_SERIES(0, DATEDIFF(DAY, @FromDate, @ToDate))
                ),
                [Ins] AS (
                    SELECT CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date) AS [Date], COUNT(*) AS [Count]
                    FROM dbo.Visits v
                    WHERE v.GateInAtUtc >= @FromUtc AND v.GateInAtUtc < @ToUtc
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                    GROUP BY CAST(DATEADD(HOUR, 6, v.GateInAtUtc) AS date)
                ),
                [Outs] AS (
                    SELECT CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date) AS [Date], COUNT(*) AS [Count]
                    FROM dbo.Visits v
                    WHERE v.GateOutAtUtc >= @FromUtc AND v.GateOutAtUtc < @ToUtc
                      AND (@ShippingLineId IS NULL OR v.ShippingLineId = @ShippingLineId)
                    GROUP BY CAST(DATEADD(HOUR, 6, v.GateOutAtUtc) AS date)
                )
                SELECT d.[Date], ISNULL(i.[Count], 0) AS GateIns, ISNULL(o.[Count], 0) AS GateOuts
                FROM [Days] d
                LEFT JOIN [Ins] i ON i.[Date] = d.[Date]
                LEFT JOIN [Outs] o ON o.[Date] = d.[Date]
                ORDER BY d.[Date]
                -- Compile with the real dates: the bounds sit in local variables, which the optimizer cannot see
                -- when it caches a plan, so it guessed 16.4% of the table for any range and chose a serial plan.
                OPTION (RECOMPILE);
            END
            """;

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
                ORDER BY AvgDwellDays DESC, l.Code;
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

        private const string PreviousRevenueByShippingLine = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                DECLARE @FromUtc datetime2 = DATEADD(HOUR, -6, CAST(@FromDate AS datetime2));
                DECLARE @ToUtc datetime2 = DATEADD(HOUR, -6, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2));  -- exclusive

                WITH [PerLine] AS (
                    SELECT i.ShippingLineId,
                           COUNT(*) AS Invoices,
                           SUM(i.Total) AS TotalBilled,
                           SUM(CASE WHEN i.Status = 2 THEN i.Total ELSE CAST(0 AS decimal(18, 2)) END) AS TotalPaid
                    FROM dbo.Invoices i
                    WHERE i.IssuedAtUtc >= @FromUtc AND i.IssuedAtUtc < @ToUtc
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

        private const string CurrentRevenueByShippingLine = """
            CREATE PROCEDURE [dbo].[__NAME__]
                @FromDate date,
                @ToDate date
            AS
            BEGIN
                SET NOCOUNT ON;
                -- Local time = UTC + 6 hours (no daylight saving). @FromDate/@ToDate are local dates, inclusive.
                DECLARE @FromUtc datetime2 = DATEADD(HOUR, -6, CAST(@FromDate AS datetime2));
                DECLARE @ToUtc datetime2 = DATEADD(HOUR, -6, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2));  -- exclusive

                WITH [PerLine] AS (
                    SELECT i.ShippingLineId,
                           COUNT(*) AS Invoices,
                           SUM(i.Total) AS TotalBilled,
                           SUM(CASE WHEN i.Status = 2 THEN i.Total ELSE CAST(0 AS decimal(18, 2)) END) AS TotalPaid
                    FROM dbo.Invoices i
                    WHERE i.IssuedAtUtc >= @FromUtc AND i.IssuedAtUtc < @ToUtc
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
                ORDER BY CASE WHEN [ShippingLineId] IS NULL THEN 1 ELSE 0 END, [Code]
                -- Compile with the real dates: the bounds sit in local variables, which the optimizer cannot see
                -- when it caches a plan, so it guessed 16.4% of the table for any range and chose a serial plan.
                OPTION (RECOMPILE);
            END
            """;
    }
}
