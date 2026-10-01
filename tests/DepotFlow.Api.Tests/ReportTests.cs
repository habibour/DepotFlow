using System.Net;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Reports;
using DepotFlow.Application.Security;
using DepotFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DepotFlow.Api.Tests;

// Covers spec S3-01, S3-02, S3-07 (the four reports). The dataset is described in ReportDataSet; every expected
// value below was worked out by hand from that table before the tests were run.
[Collection(ApiCollection.Name)]
public class ReportTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Reports = "/api/v1/reports";

    private async Task<HttpClient> ClientAsync(string role)
    {
        await Factory.SeedReportDataSetAsync();
        return await Factory.CreateClientAsync(role);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TestData.ReadAsync<T>(response);
    }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    // ---- Daily movements -------------------------------------------------------------------------------------

    [Fact]
    public async Task S3_T_REP_1_daily_movements_over_5_days_counts_each_local_date_and_shows_empty_days_as_zero()
    {
        var client = await ClientAsync(Roles.Admin);

        var report = await GetAsync<DailyMovementsReport>(client, $"{Reports}/daily-movements?from=2026-09-28&to=2026-10-02");

        Assert.Equal(
            [
                new DailyMovementRow(D(9, 28), 3, 2),   // gate-ins V1, V2, V7; gate-outs V1, V8
                new DailyMovementRow(D(9, 29), 1, 0),   // V3; no gate-outs
                new DailyMovementRow(D(9, 30), 0, 1),   // no gate-ins; V2 leaves at 00:10 local
                new DailyMovementRow(D(10, 1), 2, 1),   // V4, V6; V3
                new DailyMovementRow(D(10, 2), 2, 2)    // V5, V9; V4, V7
            ],
            report.Rows);
        Assert.Equal((8, 6), (report.Total.GateIns, report.Total.GateOuts));
    }

    [Fact]
    public async Task S3_T_REP_1_a_range_with_no_activity_is_all_zero_rows_not_empty()
    {
        var client = await ClientAsync(Roles.Admin);

        var report = await GetAsync<DailyMovementsReport>(client, $"{Reports}/daily-movements?from=2026-10-04&to=2026-10-05");

        Assert.Equal([new DailyMovementRow(D(10, 4), 0, 0), new DailyMovementRow(D(10, 5), 0, 0)], report.Rows);
    }

    [Fact]
    public async Task S3_T_REP_1_daily_movements_can_be_filtered_to_one_shipping_line()
    {
        var client = await ClientAsync(Roles.Admin);

        var report = await GetAsync<DailyMovementsReport>(client, $"{Reports}/daily-movements?from=2026-09-28&to=2026-10-02&shippingLineId=1");

        Assert.Equal(
            [
                new DailyMovementRow(D(9, 28), 2, 1),   // V1, V2 in; V1 out
                new DailyMovementRow(D(9, 29), 0, 0),
                new DailyMovementRow(D(9, 30), 0, 1),   // V2 out
                new DailyMovementRow(D(10, 1), 2, 0),   // V4, V6 in
                new DailyMovementRow(D(10, 2), 0, 1)    // V4 out
            ],
            report.Rows);
    }

    [Fact]
    public async Task S3_T_REP_2_a_gate_in_at_2350_dhaka_counts_on_that_day_and_one_at_0010_counts_on_the_next()
    {
        var client = await ClientAsync(Roles.Admin);

        var report = await GetAsync<DailyMovementsReport>(client, $"{Reports}/daily-movements?from=2026-10-01&to=2026-10-02");

        // V4 gate-in 1 Oct 23:50 local (17:50 UTC, 1 Oct) -> 1 Oct.  V5 gate-in 2 Oct 00:10 local (18:10 UTC, 1 Oct) -> 2 Oct.
        // Counting by UTC date would have given 3 on 1 Oct and 1 on 2 Oct.
        Assert.Equal([2, 2], report.Rows.Select(r => r.GateIns));
    }

    [Fact]
    public async Task S3_T_REP_3_both_the_from_and_the_to_date_are_included()
    {
        var client = await ClientAsync(Roles.Admin);

        var single = await GetAsync<DailyMovementsReport>(client, $"{Reports}/daily-movements?from=2026-09-28&to=2026-09-28");
        var inner = await GetAsync<DailyMovementsReport>(client, $"{Reports}/daily-movements?from=2026-09-29&to=2026-09-30");

        Assert.Equal([new DailyMovementRow(D(9, 28), 3, 2)], single.Rows);
        Assert.Equal([new DailyMovementRow(D(9, 29), 1, 0), new DailyMovementRow(D(9, 30), 0, 1)], inner.Rows);
    }

    // ---- Yard occupancy --------------------------------------------------------------------------------------

    [Fact]
    public async Task S3_T_REP_4_yard_occupancy_counts_slots_and_teu_per_block_with_a_total_row()
    {
        var client = await ClientAsync(Roles.YardPlanner);

        var report = await GetAsync<YardOccupancyReport>(client, $"{Reports}/yard-occupancy");

        // In the yard: 20 ft (V6), 20 ft (V5), 40 ft (V10) = 3 slots, 1 + 1 + 2 = 4 TEU, out of the 4 test slots.
        Assert.Equal([new YardOccupancyRow("A", 4, 3, 4, 75.0m)], report.Rows);
        Assert.Equal(new YardOccupancyRow("TOTAL", 4, 3, 4, 75.0m), report.Total);
    }

    // ---- Dwell time ------------------------------------------------------------------------------------------

    [Fact]
    public async Task S3_T_REP_5_dwell_time_average_median_and_max_per_line_match_the_hand_calculation()
    {
        var client = await ClientAsync(Roles.BillingOfficer);

        var report = await GetAsync<DwellTimeReport>(client, $"{Reports}/dwell-time?from=2026-09-28&to=2026-10-02");

        // Released with gate-out in range, dwell = local dates between + 1:
        //   MAEU: V1 = 1 (a one-day stay), V2 = 3, V4 = 2  -> 3 visits, avg 2.0, median 2.0, max 3
        //   MSCU: V3 = 3                                    -> 1 visit,  avg 3.0, median 3.0, max 3
        //   CMDU: V7 = 5, V8 = 2                            -> 2 visits, avg 3.5, median 3.5, max 5
        // (V9 leaves on 3 Oct, outside the range.) Ordered by average, highest first.
        Assert.Equal(
            [
                new DwellTimeRow(3, "CMDU", 2, 3.5m, 3.5m, 5),
                new DwellTimeRow(2, "MSCU", 1, 3.0m, 3.0m, 3),
                new DwellTimeRow(1, "MAEU", 3, 2.0m, 2.0m, 3)
            ],
            report.Rows);
    }

    [Fact]
    public async Task S3_T_REP_5_dwell_time_can_be_filtered_to_one_line()
    {
        var client = await ClientAsync(Roles.BillingOfficer);

        var report = await GetAsync<DwellTimeReport>(client, $"{Reports}/dwell-time?from=2026-09-28&to=2026-10-02&shippingLineId=1");

        Assert.Equal([new DwellTimeRow(1, "MAEU", 3, 2.0m, 2.0m, 3)], report.Rows);
    }

    // ---- Revenue ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task S3_T_REP_6_revenue_billed_paid_and_outstanding_per_line_and_grand_total()
    {
        var client = await ClientAsync(Roles.BillingOfficer);

        var report = await GetAsync<RevenueReport>(client, $"{Reports}/revenue?from=2026-09-28&to=2026-10-02");

        // Invoices issued in range (V9's is issued on 3 Oct, so it is not counted). Rows are ordered by line code.
        //   CMDU: V7 500 Paid, V8 200 Issued                  -> 2 invoices, billed 700, paid 500, outstanding 200
        //   MAEU: V1 0 (born Paid), V2 450 Issued, V4 500 Paid -> 3 invoices, billed 950, paid 500, outstanding 450
        //   MSCU: V3 600 Paid                                  -> 1 invoice,  billed 600, paid 600, outstanding 0
        Assert.Equal("BDT", report.Currency);
        Assert.Equal(
            [
                new RevenueRow(3, "CMDU", 2, 700m, 500m, 200m),
                new RevenueRow(1, "MAEU", 3, 950m, 500m, 450m),
                new RevenueRow(2, "MSCU", 1, 600m, 600m, 0m)
            ],
            report.Rows);
        Assert.Equal(new RevenueTotal(6, 2250m, 1600m, 650m), report.Total);
    }

    [Fact]
    public async Task S3_T_REP_6_revenue_uses_the_local_date_of_the_issue_time()
    {
        var client = await ClientAsync(Roles.Admin);

        // V2's invoice is issued 30 Sep 00:10 local, which is 29 Sep 18:10 UTC.
        var onThe30th = await GetAsync<RevenueReport>(client, $"{Reports}/revenue?from=2026-09-30&to=2026-09-30");
        var onThe29th = await GetAsync<RevenueReport>(client, $"{Reports}/revenue?from=2026-09-29&to=2026-09-29");

        Assert.Equal([new RevenueRow(1, "MAEU", 1, 450m, 0m, 450m)], onThe30th.Rows);
        Assert.Equal(new RevenueTotal(1, 450m, 0m, 450m), onThe30th.Total);
        Assert.Empty(onThe29th.Rows);
        Assert.Equal(new RevenueTotal(0, 0m, 0m, 0m), onThe29th.Total);
    }

    // ---- Baseline versus current ----------------------------------------------------------------------------

    [Fact]
    public async Task S3_T_REP_7_baseline_and_current_procedures_return_identical_rows_for_all_four_reports()
    {
        await Factory.SeedReportDataSetAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        var ranges = new (DateOnly From, DateOnly To)[] { (D(9, 28), D(10, 2)), (D(9, 30), D(9, 30)), (D(9, 1), D(10, 31)), (D(10, 4), D(10, 5)) };
        int?[] lines = [null, 1, 3];

        foreach (var (from, to) in ranges)
        {
            foreach (var line in lines)
            {
                var daily = await db.Database.SqlQuery<DailyMovementRow>($"EXEC dbo.usp_DailyGateMovements @FromDate = {from}, @ToDate = {to}, @ShippingLineId = {line}").ToListAsync();
                var dailyBase = await db.Database.SqlQuery<DailyMovementRow>($"EXEC dbo.usp_DailyGateMovements_Baseline @FromDate = {from}, @ToDate = {to}, @ShippingLineId = {line}").ToListAsync();
                Assert.Equal(dailyBase, daily);

                var dwell = await db.Database.SqlQuery<DwellTimeRow>($"EXEC dbo.usp_AverageDwellTime @FromDate = {from}, @ToDate = {to}, @ShippingLineId = {line}").ToListAsync();
                var dwellBase = await db.Database.SqlQuery<DwellTimeRow>($"EXEC dbo.usp_AverageDwellTime_Baseline @FromDate = {from}, @ToDate = {to}, @ShippingLineId = {line}").ToListAsync();
                Assert.Equal(dwellBase, dwell);
            }

            var revenue = await db.Database.SqlQuery<RevenueRow>($"EXEC dbo.usp_RevenueByShippingLine @FromDate = {from}, @ToDate = {to}").ToListAsync();
            var revenueBase = await db.Database.SqlQuery<RevenueRow>($"EXEC dbo.usp_RevenueByShippingLine_Baseline @FromDate = {from}, @ToDate = {to}").ToListAsync();
            Assert.Equal(revenueBase, revenue);
        }

        var yard = await db.Database.SqlQuery<YardOccupancyRow>($"EXEC dbo.usp_YardOccupancy").ToListAsync();
        var yardBase = await db.Database.SqlQuery<YardOccupancyRow>($"EXEC dbo.usp_YardOccupancy_Baseline").ToListAsync();
        Assert.Equal(yardBase, yard);
        Assert.NotEmpty(yard);
    }

    // ---- Validation and roles --------------------------------------------------------------------------------

    [Theory]
    [InlineData("daily-movements?from=2026-10-02&to=2026-09-28")]   // from after to
    [InlineData("daily-movements?to=2026-09-28")]                   // missing from
    [InlineData("daily-movements?from=2026-09-28")]                 // missing to
    [InlineData("daily-movements?from=2025-01-01&to=2026-09-28")]   // more than 366 days
    [InlineData("dwell-time?from=2020-01-01&to=2026-09-28")]        // more than 1,830 days
    [InlineData("dwell-time?from=2026-10-02&to=2026-09-28")]
    [InlineData("revenue?from=2020-01-01&to=2026-09-28")]
    [InlineData("revenue?from=2026-10-02")]
    [InlineData("revenue?from=not-a-date&to=2026-10-02")]
    public async Task S3_T_REP_8_invalid_ranges_give_400(string path)
    {
        var client = await Factory.CreateClientAsync(Roles.Admin);

        var response = await client.GetAsync($"{Reports}/{path}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task S3_T_REP_8_a_366_day_daily_range_is_accepted_and_367_is_not()
    {
        var client = await Factory.CreateClientAsync(Roles.Admin);

        var ok = await client.GetAsync($"{Reports}/daily-movements?from=2026-01-01&to=2027-01-01");       // 366 days
        var tooLong = await client.GetAsync($"{Reports}/daily-movements?from=2026-01-01&to=2027-01-02");  // 367 days

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Theory]
    [InlineData(Roles.GateClerk, "revenue?from=2026-09-28&to=2026-10-02", HttpStatusCode.Forbidden)]
    [InlineData(Roles.YardPlanner, "revenue?from=2026-09-28&to=2026-10-02", HttpStatusCode.Forbidden)]
    [InlineData(Roles.BillingOfficer, "daily-movements?from=2026-09-28&to=2026-10-02", HttpStatusCode.Forbidden)]
    [InlineData(Roles.BillingOfficer, "yard-occupancy", HttpStatusCode.Forbidden)]
    [InlineData(Roles.GateClerk, "dwell-time?from=2026-09-28&to=2026-10-02", HttpStatusCode.Forbidden)]
    [InlineData(Roles.Admin, "revenue?from=2026-09-28&to=2026-10-02", HttpStatusCode.OK)]
    [InlineData(Roles.BillingOfficer, "revenue?from=2026-09-28&to=2026-10-02", HttpStatusCode.OK)]
    [InlineData(Roles.GateClerk, "daily-movements?from=2026-09-28&to=2026-10-02", HttpStatusCode.OK)]
    [InlineData(Roles.GateClerk, "yard-occupancy", HttpStatusCode.OK)]
    [InlineData(Roles.YardPlanner, "dwell-time?from=2026-09-28&to=2026-10-02", HttpStatusCode.OK)]
    public async Task S3_T_REP_9_roles_are_enforced(string role, string path, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAsync(role);

        var response = await client.GetAsync($"{Reports}/{path}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Reports_need_a_token()
    {
        var response = await Factory.CreateClient().GetAsync($"{Reports}/yard-occupancy");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
