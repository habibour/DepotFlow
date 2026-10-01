using DepotFlow.Application;
using DepotFlow.Domain;
using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DepotFlow.Api.Tests.Support;

/// <summary>
/// A small hand-made dataset for the report tests. All times below are Asia/Dhaka local times (UTC+6); the helper
/// converts them to the UTC values the database stores. Expected report values are worked out by hand in ReportTests.
///
///   id  line  size  gate-in (local)   gate-out (local)  dwell  invoice (rate/day, free) -> total, status
///   V1  1     20    09-28 10:00       09-28 17:00       1      free 4              -> 0,   Paid (born paid)
///   V2  1     20    09-28 23:50       09-30 00:10       3      150 -> 450,  Issued       (crosses two local midnights)
///   V3  2     40    09-29 08:00       10-01 12:00       3      200 -> 600,  Paid
///   V4  1     40    10-01 23:50       10-02 09:00       2      250 -> 500,  Paid          (gate-in at 23:50: counts on 1 Oct)
///   V5  2     20    10-02 00:10       in yard                                                (00:10 local is still 1 Oct in UTC)
///   V6  1     20    10-01 08:00       in yard
///   V7  3     40    09-28 08:00       10-02 08:00       5      100 -> 500,  Paid
///   V8  3     20    09-27 22:00       09-28 06:00       2      100 -> 200,  Issued        (gate-in before the 5-day range)
///   V9  2     20    10-02 20:00       10-03 08:00       2      100 -> 200,  Issued        (gate-out after the range)
///   V10 1     40    09-27 12:00       in yard                                                (gate-in before the range)
///
/// In the yard: V6 (A-01-01-1, 20 ft), V5 (A-01-01-2, 20 ft), V10 (A-01-02-1, 40 ft).
/// Shipping lines: 1 = MAEU, 2 = MSCU, 3 = CMDU (the seeded codes).
/// </summary>
public static class ReportDataSet
{
    public static DateTime Dhaka(int month, int day, int hour, int minute) =>
        new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-6);

    public static async Task SeedReportDataSetAsync(this ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        var lines = await db.ShippingLines.Where(l => l.Id <= 3).ToDictionaryAsync(l => l.Id);
        var slots = await db.YardSlots
            .OrderBy(s => s.Block).ThenBy(s => s.Row).ThenBy(s => s.Bay).ThenBy(s => s.Tier)
            .ToListAsync();
        var createdAt = Dhaka(9, 1, 0, 0);

        Visit NewVisit(int lineId, int size, DateTime gateInUtc)
        {
            ContainerNumber.TryCreate(TestData.NewContainerNumber(), out var number, out _);
            var container = new Container(number!, size, createdAt);
            return new Visit(container, lines[lineId], gateInUtc, "TRUCK-IN", "SEAL-1", null, "report-test");
        }

        Visit Released(int lineId, int size, DateTime inUtc, DateTime outUtc, int freeDays, decimal rate, bool paid)
        {
            var visit = NewVisit(lineId, size, inUtc);
            visit.GateOut(outUtc, "TRUCK-OUT", null, "report-test");

            var dwell = DwellCalculator.Days(inUtc, outUtc, DepotTimeZone.Dhaka);
            var tariff = new TariffSnapshot("Flat", freeDays, "BDT", [new TariffTierSnapshot(freeDays + 1, null, rate)]);
            var invoice = Invoice.Issue(visit, tariff, dwell, new FlatStrategy().Calculate(tariff, dwell), outUtc);
            if (paid && invoice.Status != InvoiceStatus.Paid)
            {
                invoice.Pay(outUtc.AddHours(1), "report-test");
            }

            db.Invoices.Add(invoice);   // adds the visit and container with it
            return visit;
        }

        Visit InYard(int lineId, int size, DateTime inUtc, YardSlot slot)
        {
            var visit = NewVisit(lineId, size, inUtc);
            visit.AssignSlot(slot);
            db.Visits.Add(visit);
            return visit;
        }

        Released(1, 20, Dhaka(9, 28, 10, 0), Dhaka(9, 28, 17, 0), freeDays: 4, rate: 100m, paid: true);    // V1
        Released(1, 20, Dhaka(9, 28, 23, 50), Dhaka(9, 30, 0, 10), freeDays: 0, rate: 150m, paid: false);  // V2
        Released(2, 40, Dhaka(9, 29, 8, 0), Dhaka(10, 1, 12, 0), freeDays: 0, rate: 200m, paid: true);     // V3
        Released(1, 40, Dhaka(10, 1, 23, 50), Dhaka(10, 2, 9, 0), freeDays: 0, rate: 250m, paid: true);    // V4
        InYard(2, 20, Dhaka(10, 2, 0, 10), slots[1]);                                                      // V5
        InYard(1, 20, Dhaka(10, 1, 8, 0), slots[0]);                                                       // V6
        Released(3, 40, Dhaka(9, 28, 8, 0), Dhaka(10, 2, 8, 0), freeDays: 0, rate: 100m, paid: true);     // V7
        Released(3, 20, Dhaka(9, 27, 22, 0), Dhaka(9, 28, 6, 0), freeDays: 0, rate: 100m, paid: false);   // V8
        Released(2, 20, Dhaka(10, 2, 20, 0), Dhaka(10, 3, 8, 0), freeDays: 0, rate: 100m, paid: false);   // V9
        InYard(1, 40, Dhaka(9, 27, 12, 0), slots[2]);                                                      // V10

        await db.SaveChangesAsync();
    }
}
