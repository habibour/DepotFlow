using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Problem: dwell time and the daily gate-out count read every Visits page (59,020 to 66,722 logical reads) even for a
    /// 30-day range, because no index is ordered by gate-out time. Change: a filtered covering index on released visits,
    /// and the daily proc states Status = 2 so the filtered index qualifies.
    /// </summary>
    public partial class AddReleasedVisitsGateOutIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Visits_Released_GateOut_Covering",
                table: "Visits",
                column: "GateOutAtUtc",
                filter: "[Status] = 2")
                .Annotation("SqlServer:Include", new[] { "GateInAtUtc", "ShippingLineId" });

            migrationBuilder.Sql(CurrentDailyGateMovements.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_DailyGateMovements"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreviousDailyGateMovements.Replace("CREATE PROCEDURE", "ALTER PROCEDURE").Replace("__NAME__", "usp_DailyGateMovements"));

            migrationBuilder.DropIndex(
                name: "IX_Visits_Released_GateOut_Covering",
                table: "Visits");
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
                ORDER BY d.[Date]
                -- Compile with the real dates: the bounds sit in local variables, which the optimizer cannot see
                -- when it caches a plan, so it guessed 16.4% of the table for any range and chose a serial plan.
                OPTION (RECOMPILE);
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
                    -- Status = 2 always accompanies a gate-out time (only Visit.GateOut sets either); stating it lets the
                    -- filtered index on released visits be used.
                    WHERE v.Status = 2 AND v.GateOutAtUtc >= @FromUtc AND v.GateOutAtUtc < @ToUtc
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
    }
}
