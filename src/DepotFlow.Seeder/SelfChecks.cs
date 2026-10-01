using DepotFlow.Application;
using DepotFlow.Domain;
using DepotFlow.Domain.Billing;
using Microsoft.Data.SqlClient;

namespace DepotFlow.Seeder;

/// <summary>Checks the loaded data against the spec's acceptance rules and prints a summary. Returns false if any check failed.</summary>
internal static class SelfChecks
{
    public static async Task<bool> RunAsync(
        SqlConnection connection, SeederOptions options, IReadOnlyDictionary<(int, int), TariffSnapshot> tariffs, Action<string> log)
    {
        var ok = true;

        bool Report(string name, bool passed, string detail)
        {
            log($"  [{(passed ? "PASS" : "FAIL")}] {name}: {detail}");
            ok &= passed;
            return passed;
        }

        log("Row counts");
        foreach (var table in new[] { "ShippingLines", "Tariffs", "YardSlots", "Containers", "Visits", "Invoices" })
        {
            log($"  {table,-14} {await ScalarAsync<int>(connection, $"SELECT COUNT(*) FROM dbo.{table}"):N0}");
        }

        var visits = await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.Visits");
        var inYard = await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.Visits WHERE Status = 1");
        var invoices = await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.Invoices");
        var paid = await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.Invoices WHERE Status = 2");
        log($"  visits in yard {inYard:N0}, released {visits - inYard:N0}; invoices paid {paid:N0} ({100.0 * paid / Math.Max(invoices, 1):F1}%)");

        log("Self-checks");
        var deviation = Math.Abs(visits - options.Visits) / (double)options.Visits;
        Report("visit count within 1% of the request", deviation <= 0.01, $"{visits:N0} of {options.Visits:N0} ({100 * deviation:F2}% off)");

        var dupActive = await ScalarAsync<int>(connection,
            "SELECT COUNT(*) FROM (SELECT ContainerId FROM dbo.Visits WHERE Status = 1 GROUP BY ContainerId HAVING COUNT(*) > 1) x");
        Report("no container has two active visits", dupActive == 0, $"{dupActive} violations");

        var sharedSlots = await ScalarAsync<int>(connection,
            "SELECT COUNT(*) FROM (SELECT YardSlotId FROM dbo.Visits WHERE Status = 1 AND YardSlotId IS NOT NULL GROUP BY YardSlotId HAVING COUNT(*) > 1) x");
        Report("no two active visits share a slot", sharedSlots == 0, $"{sharedSlots} violations");

        var unsupported = await ScalarAsync<int>(connection, """
            SELECT COUNT(*)
            FROM dbo.Visits v
            JOIN dbo.YardSlots s ON s.Id = v.YardSlotId
            WHERE v.Status = 1 AND s.Tier > 1
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.Visits v2 JOIN dbo.YardSlots s2 ON s2.Id = v2.YardSlotId
                  WHERE v2.Status = 1 AND s2.Block = s.Block AND s2.Row = s.Row AND s2.Bay = s.Bay AND s2.Tier = s.Tier - 1)
            """);
        Report("every stacked container has one under it (stacking rule)", unsupported == 0, $"{unsupported} violations");

        var overlaps = await ScalarAsync<int>(connection, """
            SELECT COUNT(*)
            FROM dbo.Visits a
            JOIN dbo.Visits b ON b.ContainerId = a.ContainerId AND b.Id > a.Id
            WHERE a.GateInAtUtc < ISNULL(b.GateOutAtUtc, '9999-12-31') AND b.GateInAtUtc < ISNULL(a.GateOutAtUtc, '9999-12-31')
            """);
        Report("no container has overlapping visits", overlaps == 0, $"{overlaps} violations");

        var invalidNumbers = 0;
        var checkedNumbers = 0;
        await using (var command = new SqlCommand("SELECT Number FROM dbo.Containers", connection) { CommandTimeout = 0 })
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                checkedNumbers++;
                if (!ContainerNumber.TryCreate(reader.GetString(0), out _, out _))
                {
                    invalidNumbers++;
                }
            }
        }

        Report("every container number passes validation", invalidNumbers == 0, $"{checkedNumbers:N0} checked, {invalidNumbers} invalid");

        // Recompute the invoice total of a deterministic sample of about 1,000 invoices with the real domain code.
        var step = Math.Max(1, invoices / 1000);
        var wrong = 0;
        var sampled = 0;
        await using (var command = new SqlCommand("""
            SELECT i.Total, i.DwellDays, i.ShippingLineId, c.SizeFeet, v.GateInAtUtc, v.GateOutAtUtc
            FROM dbo.Invoices i
            JOIN dbo.Visits v ON v.Id = i.VisitId
            JOIN dbo.Containers c ON c.Id = v.ContainerId
            WHERE i.Id % @step = 0
            """, connection) { CommandTimeout = 0 })
        {
            command.Parameters.AddWithValue("@step", step);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sampled++;
                var tariff = tariffs[(reader.GetInt32(2), reader.GetByte(3))];
                var dwell = DwellCalculator.Days(reader.GetDateTime(4), reader.GetDateTime(5), DepotTimeZone.Dhaka);
                var expected = TariffStrategyFactory.For(tariff.StrategyKey).Calculate(tariff, dwell).Total;
                if (expected != reader.GetDecimal(0) || dwell != reader.GetInt32(1))
                {
                    wrong++;
                }
            }
        }

        Report("invoice totals match a recomputation with the real strategies", wrong == 0, $"{sampled:N0} sampled, {wrong} differ");

        log("Dwell days of released visits (calendar days, local time)");
        await using (var command = new SqlCommand("""
            SELECT CASE WHEN DwellDays = 1 THEN '1' WHEN DwellDays <= 3 THEN '2-3' WHEN DwellDays <= 7 THEN '4-7'
                        WHEN DwellDays <= 14 THEN '8-14' WHEN DwellDays <= 30 THEN '15-30' ELSE '31-60' END AS Bucket,
                   COUNT(*) AS N, MIN(DwellDays) AS MinD
            FROM dbo.Invoices GROUP BY CASE WHEN DwellDays = 1 THEN '1' WHEN DwellDays <= 3 THEN '2-3' WHEN DwellDays <= 7 THEN '4-7'
                        WHEN DwellDays <= 14 THEN '8-14' WHEN DwellDays <= 30 THEN '15-30' ELSE '31-60' END
            ORDER BY MIN(DwellDays)
            """, connection) { CommandTimeout = 0 })
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                log($"  {reader.GetString(0),6} days: {reader.GetInt32(1),9:N0}  ({100.0 * reader.GetInt32(1) / Math.Max(invoices, 1):F1}%)");
            }
        }

        var stats = await RowAsync(connection, "SELECT AVG(CAST(DwellDays AS float)), MIN(DwellDays), MAX(DwellDays) FROM dbo.Invoices");
        log($"  mean {Convert.ToDouble(stats[0]):F1}, min {stats[1]}, max {stats[2]}");
        Report("dwell days within 1 to 60", Convert.ToInt32(stats[1]) >= 1 && Convert.ToInt32(stats[2]) <= 60, $"min {stats[1]}, max {stats[2]}");

        return ok;
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static async Task<object[]> RowAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        return values;
    }
}
