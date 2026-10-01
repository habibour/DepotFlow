using System.Data;
using DepotFlow.Domain.Billing;
using Microsoft.Data.SqlClient;

namespace DepotFlow.Seeder;

internal sealed record LoadContext(
    string GateClerkUserId,
    string BillingUserId,
    IReadOnlyDictionary<(int ShippingLineId, int SizeFeet), TariffSnapshot> Tariffs,
    int Seed);

/// <summary>
/// Loads the generated data with SqlBulkCopy in batches of 50,000 rows. Ids are written explicitly (KeepIdentity),
/// so visits and invoices can point at each other without a round trip. Bulk loading bypasses EF, so the audit
/// interceptor does not run: InvoiceLines and AuditLogs are deliberately not seeded.
/// </summary>
internal static class BulkLoader
{
    private const int BatchSize = 50_000;
    private static readonly TimeSpan DhakaOffset = TimeSpan.FromHours(6);

    public static async Task LoadContainersAsync(SqlConnection connection, GeneratedData data, DateTime createdAtUtc, Action<string> log)
    {
        using var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Number", typeof(string));
        table.Columns.Add("SizeFeet", typeof(byte));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        using var bulk = Create(connection, "Containers", table);
        for (var c = 0; c < data.ContainerNumbers.Length; c++)
        {
            table.Rows.Add(c + 1, data.ContainerNumbers[c], data.ContainerSizes[c], createdAtUtc);
            await FlushAsync(bulk, table, force: false);
        }

        await FlushAsync(bulk, table, force: true);
        log($"  containers: {data.ContainerNumbers.Length:N0} rows");
    }

    public static async Task LoadVisitsAsync(SqlConnection connection, GeneratedData data, LoadContext context, Action<string> log)
    {
        using var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("ContainerId", typeof(int));
        table.Columns.Add("ShippingLineId", typeof(int));
        table.Columns.Add("Status", typeof(byte));
        table.Columns.Add("GateInAtUtc", typeof(DateTime));
        table.Columns.Add("GateOutAtUtc", typeof(DateTime));
        table.Columns.Add("TruckInNumber", typeof(string));
        table.Columns.Add("TruckOutNumber", typeof(string));
        table.Columns.Add("SealNumber", typeof(string));
        table.Columns.Add("DamageNotesIn", typeof(string));
        table.Columns.Add("DamageNotesOut", typeof(string));
        table.Columns.Add("YardSlotId", typeof(int));
        table.Columns.Add("CreatedByUserId", typeof(string));
        table.Columns.Add("ClosedByUserId", typeof(string));

        var rng = new Random(context.Seed + 2);
        using var bulk = Create(connection, "Visits", table);
        for (var i = 0; i < data.Visits.Length; i++)
        {
            var v = data.Visits[i];
            var released = v.GateOutTicks != 0;
            table.Rows.Add(
                (long)i + 1,
                v.ContainerId,
                v.ShippingLineId,
                released ? (byte)2 : (byte)1,
                new DateTime(v.GateInTicks, DateTimeKind.Utc),
                released ? new DateTime(v.GateOutTicks, DateTimeKind.Utc) : DBNull.Value,
                TruckNumber(rng),
                released ? TruckNumber(rng) : DBNull.Value,
                $"SL{rng.Next(100000, 999999)}",
                DBNull.Value,
                DBNull.Value,
                v.SlotId == 0 ? DBNull.Value : v.SlotId,
                context.GateClerkUserId,
                released ? context.GateClerkUserId : DBNull.Value);
            await FlushAsync(bulk, table, force: false, progress: i + 1, total: data.Visits.Length, log);
        }

        await FlushAsync(bulk, table, force: true);
        log($"  visits: {data.Visits.Length:N0} rows");
    }

    public static async Task<int> LoadInvoicesAsync(SqlConnection connection, GeneratedData data, LoadContext context, Action<string> log)
    {
        using var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("VisitId", typeof(long));
        table.Columns.Add("ShippingLineId", typeof(int));
        table.Columns.Add("IssuedAtUtc", typeof(DateTime));
        table.Columns.Add("DwellDays", typeof(int));
        table.Columns.Add("FreeDays", typeof(int));
        table.Columns.Add("StrategyKey", typeof(string));
        table.Columns.Add("Currency", typeof(string));
        table.Columns.Add("Total", typeof(decimal));
        table.Columns.Add("Status", typeof(byte));
        table.Columns.Add("PaidAtUtc", typeof(DateTime));
        table.Columns.Add("PaidByUserId", typeof(string));

        var rng = new Random(context.Seed + 1);
        var recentCutoff = data.AsOfUtc.AddDays(-30);
        long invoiceId = 0;

        // InvoiceNumber is not mapped: the column default draws the next value of the invoice sequence.
        using var bulk = Create(connection, "Invoices", table);
        for (var i = 0; i < data.Visits.Length; i++)
        {
            var v = data.Visits[i];
            if (v.GateOutTicks == 0)
            {
                continue;   // still in the yard: no invoice yet
            }

            var tariff = context.Tariffs[(v.ShippingLineId, data.ContainerSizes[v.ContainerId - 1])];
            var dwell = LocalDwellDays(v.GateInTicks, v.GateOutTicks);
            var total = TariffStrategyFactory.For(tariff.StrategyKey).Calculate(tariff, dwell).Total;

            var issuedAt = new DateTime(v.GateOutTicks, DateTimeKind.Utc);
            // 85% of invoices end up paid overall; visits released in the last 30 days are mostly still unpaid.
            var paid = total == 0 || rng.NextDouble() < (issuedAt >= recentCutoff ? 0.30 : 0.83);
            var paidAt = paid ? Min(issuedAt.AddDays(rng.NextDouble() * 20), data.AsOfUtc) : (DateTime?)null;

            table.Rows.Add(
                ++invoiceId,
                (long)i + 1,
                v.ShippingLineId,
                issuedAt,
                dwell,
                tariff.FreeDays,
                tariff.StrategyKey,
                tariff.Currency,
                total,
                paid ? (byte)2 : (byte)1,
                paidAt is null ? DBNull.Value : paidAt.Value,
                paid ? context.BillingUserId : DBNull.Value);
            await FlushAsync(bulk, table, force: false, progress: (int)invoiceId, total: data.Visits.Length - data.InYardCount, log);
        }

        await FlushAsync(bulk, table, force: true);
        log($"  invoices: {invoiceId:N0} rows");
        return (int)invoiceId;
    }

    /// <summary>Calendar days between the Dhaka-local gate-in and gate-out dates, plus one (Dhaka is UTC+6 all year).</summary>
    internal static int LocalDwellDays(long gateInTicks, long gateOutTicks) =>
        (int)(new DateTime(gateOutTicks).Add(DhakaOffset).Date - new DateTime(gateInTicks).Add(DhakaOffset).Date).TotalDays + 1;

    private static SqlBulkCopy Create(SqlConnection connection, string tableName, DataTable shape)
    {
        var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = $"dbo.{tableName}",
            BatchSize = BatchSize,
            BulkCopyTimeout = 0   // large loads must not time out
        };

        foreach (DataColumn column in shape.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        return bulk;
    }

    private static Task FlushAsync(SqlBulkCopy bulk, DataTable table, bool force, int progress = 0, int total = 0, Action<string>? log = null) =>
        table.Rows.Count >= BatchSize || (force && table.Rows.Count > 0) ? WriteAsync(bulk, table, progress, total, log) : Task.CompletedTask;

    private static async Task WriteAsync(SqlBulkCopy bulk, DataTable table, int progress, int total, Action<string>? log)
    {
        await bulk.WriteToServerAsync(table);
        table.Clear();
        if (log is not null && total > 0 && progress % (BatchSize * 5) < BatchSize)
        {
            log($"    {progress:N0} / {total:N0}");
        }
    }

    private static string TruckNumber(Random rng) =>
        $"DHAKA-{(char)('A' + rng.Next(26))}{(char)('A' + rng.Next(26))}-{rng.Next(11, 99)}-{rng.Next(1000, 9999)}";

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
