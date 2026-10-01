using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace DepotFlow.Seeder;

/// <summary>
/// The report benchmark (spec S3-04). For every report and parameter set it runs the baseline and the current
/// stored procedure and prints a Markdown report: median and p95 wall-clock time, logical reads on the largest
/// table, the execution plan in words, and whether both versions returned identical rows.
/// Nothing here is estimated: every number printed comes from a run made by this harness.
/// </summary>
internal static partial class Benchmark
{
    private const int TimedRuns = 10;

    private sealed record Case(string Report, string ParameterSet, string Procedure, DateOnly? From, DateOnly? To, int? ShippingLineId, bool UsesLine);

    private sealed record Measurement(
        double MedianMs, double P95Ms, string ResultHash, int Rows, string LargestTable, long LogicalReads, string Plan);

    [GeneratedRegex(@"Table '(?<table>[^']+)'\. Scan count (?<scans>\d+), logical reads (?<reads>\d+)")]
    private static partial Regex StatisticsIo();

    public static async Task<int> RunAsync(string connectionString, DateOnly asOf, string? only, Action<string> log)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var cases = BuildCases(asOf)
            .Where(c => only is null || c.Report.Contains(only, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (cases.Count == 0)
        {
            throw new ArgumentException($"--only '{only}' matches no report.");
        }

        var output = new StringBuilder();
        void Out(string line = "") { output.AppendLine(line); Console.WriteLine(line); }

        await PrintEnvironmentAsync(connection, asOf, Out);

        var allIdentical = true;
        foreach (var report in cases.GroupBy(c => c.Report))
        {
            Out();
            Out($"### {report.Key}");
            Out();
            Out("| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |");
            Out("|---|---:|---:|---:|---:|---|---:|:---:|");

            var plans = new List<string>();
            foreach (var c in report)
            {
                log($"  measuring {c.Report} / {c.ParameterSet} ...");
                var baseline = await MeasureAsync(connection, c, c.Procedure + "_Baseline");
                var current = await MeasureAsync(connection, c, c.Procedure);
                var same = baseline.ResultHash == current.ResultHash;
                allIdentical &= same;

                Out($"| {c.ParameterSet} | {baseline.MedianMs:F0} | {baseline.P95Ms:F0} | {current.MedianMs:F0} | {current.P95Ms:F0} " +
                    $"| {baseline.LargestTable} {baseline.LogicalReads:N0} -> {current.LargestTable} {current.LogicalReads:N0} | {current.Rows} | {(same ? "yes" : "**NO**")} |");
                plans.Add($"- {c.ParameterSet}: baseline plan = {baseline.Plan}; current plan = {current.Plan}");
            }

            Out();
            Out("Plans (main operators of the actual execution plan):");
            plans.ForEach(p => Out(p));
        }

        Out();
        Out(allIdentical
            ? "All baseline and current procedures returned identical results."
            : "WARNING: at least one current procedure returned different rows than its baseline.");

        return allIdentical ? 0 : 1;
    }

    private static List<Case> BuildCases(DateOnly asOf)
    {
        var last30 = (From: asOf.AddDays(-29), To: asOf);
        var last12 = (From: asOf.AddMonths(-12), To: asOf);
        var all36 = (From: asOf.AddMonths(-36), To: asOf);

        return
        [
            new("Daily gate movements", "last 30 days, all lines", "usp_DailyGateMovements", last30.From, last30.To, null, true),
            new("Daily gate movements", "last 12 months, all lines", "usp_DailyGateMovements", last12.From, last12.To, null, true),
            new("Daily gate movements", "last 12 months, one line", "usp_DailyGateMovements", last12.From, last12.To, 1, true),
            new("Yard occupancy", "no parameters", "usp_YardOccupancy", null, null, null, false),
            new("Average dwell time", "last 30 days", "usp_AverageDwellTime", last30.From, last30.To, null, true),
            new("Average dwell time", "last 12 months", "usp_AverageDwellTime", last12.From, last12.To, null, true),
            new("Average dwell time", "all 36 months", "usp_AverageDwellTime", all36.From, all36.To, null, true),
            new("Revenue by shipping line", "last 30 days", "usp_RevenueByShippingLine", last30.From, last30.To, null, false),
            new("Revenue by shipping line", "last 12 months", "usp_RevenueByShippingLine", last12.From, last12.To, null, false),
            new("Revenue by shipping line", "all 36 months", "usp_RevenueByShippingLine", all36.From, all36.To, null, false)
        ];
    }

    private static async Task<Measurement> MeasureAsync(SqlConnection connection, Case c, string procedure)
    {
        // 1. one run, discarded: warms the buffer cache and the plan cache
        await ExecuteAsync(connection, c, procedure);

        // 2. ten timed runs
        var times = new List<double>();
        (string Hash, int Rows) result = ("", 0);
        for (var i = 0; i < TimedRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            result = await ExecuteAsync(connection, c, procedure);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        var median = (times[TimedRuns / 2 - 1] + times[TimedRuns / 2]) / 2.0;
        var p95 = times[(int)Math.Ceiling(0.95 * TimedRuns) - 1];

        // 3 and 4. one instrumented run: logical reads (STATISTICS IO) and the actual plan (STATISTICS XML)
        var (table, reads, plan) = await InstrumentedRunAsync(connection, c, procedure);

        return new Measurement(median, p95, result.Hash, result.Rows, table, reads, plan);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, Case c, string procedure)
    {
        var command = new SqlCommand($"dbo.{procedure}", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 0 };
        if (c.From is not null)
        {
            command.Parameters.Add("@FromDate", SqlDbType.Date).Value = c.From.Value.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add("@ToDate", SqlDbType.Date).Value = c.To!.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (c.UsesLine)
        {
            command.Parameters.Add("@ShippingLineId", SqlDbType.Int).Value = (object?)c.ShippingLineId ?? DBNull.Value;
        }

        return command;
    }

    /// <summary>Runs the procedure, reads every row, and returns a hash of the rows plus the row count.</summary>
    private static async Task<(string Hash, int Rows)> ExecuteAsync(SqlConnection connection, Case c, string procedure)
    {
        await using var command = CreateCommand(connection, c, procedure);
        await using var reader = await command.ExecuteReaderAsync();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rows = 0;
        var values = new object[reader.FieldCount];
        while (await reader.ReadAsync())
        {
            reader.GetValues(values);
            var line = string.Join('|', values.Select(v => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "NULL"));
            hash.AppendData(Encoding.UTF8.GetBytes(line + "\n"));
            rows++;
        }

        return (Convert.ToHexString(hash.GetHashAndReset())[..16], rows);
    }

    private static async Task<(string Table, long Reads, string Plan)> InstrumentedRunAsync(SqlConnection connection, Case c, string procedure)
    {
        var reads = new Dictionary<string, long>();
        void OnInfo(object _, SqlInfoMessageEventArgs e)
        {
            foreach (Match m in StatisticsIo().Matches(e.Message))
            {
                var table = m.Groups["table"].Value;
                reads[table] = reads.GetValueOrDefault(table) + long.Parse(m.Groups["reads"].Value, CultureInfo.InvariantCulture);
            }
        }

        connection.InfoMessage += OnInfo;
        var operators = new List<string>();
        try
        {
            await using (var on = new SqlCommand("SET STATISTICS IO ON; SET STATISTICS XML ON;", connection)) { await on.ExecuteNonQueryAsync(); }

            await using (var command = CreateCommand(connection, c, procedure))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                do
                {
                    // Result sets whose first column is a showplan are execution plans; the others are the report rows.
                    if (reader.FieldCount == 1 && reader.GetName(0).StartsWith("Microsoft SQL Server", StringComparison.Ordinal))
                    {
                        while (await reader.ReadAsync())
                        {
                            operators.AddRange(DescribePlan(reader.GetString(0)));
                        }
                    }
                    else
                    {
                        while (await reader.ReadAsync()) { }
                    }
                }
                while (await reader.NextResultAsync());
            }

            await using (var off = new SqlCommand("SET STATISTICS IO OFF; SET STATISTICS XML OFF;", connection)) { await off.ExecuteNonQueryAsync(); }
        }
        finally
        {
            connection.InfoMessage -= OnInfo;
        }

        var real = reads.Where(kv => !kv.Key.StartsWith("Work", StringComparison.Ordinal)).OrderByDescending(kv => kv.Value).FirstOrDefault();
        var plan = string.Join(", ", operators.Distinct().Take(8));
        return (real.Key ?? "-", real.Value, plan.Length == 0 ? "-" : plan);
    }

    /// <summary>Turns a showplan XML into short phrases: "Index Seek (Invoices.IX_...)", "Hash Match", and so on.</summary>
    private static IEnumerable<string> DescribePlan(string showplan)
    {
        var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2004/07/showplan");
        var doc = XDocument.Parse(showplan);
        foreach (var op in doc.Descendants(ns + "RelOp"))
        {
            var physical = (string?)op.Attribute("PhysicalOp") ?? "?";
            if (physical is "Compute Scalar" or "Parallelism" or "Sort" or "Segment" or "Sequence Project" or "Top" or "Nested Loops" or "Filter" or "Stream Aggregate" or "Table-valued function" or "Constant Scan" or "Concatenation")
            {
                continue;   // plumbing; the scans, seeks and joins tell the story
            }

            var obj = op.Elements(ns + "IndexScan").Concat(op.Elements(ns + "TableScan")).Elements(ns + "Object").FirstOrDefault();
            yield return obj is null
                ? physical
                : $"{physical} ({Clean(obj.Attribute("Table"))}.{(obj.Attribute("Index") is { } index ? Clean(index) : "heap")})";
        }
    }

    private static string Clean(XAttribute? attribute) => ((string?)attribute)?.Trim('[', ']') ?? "?";

    private static async Task PrintEnvironmentAsync(SqlConnection connection, DateOnly asOf, Action<string> print)
    {
        string Scalar(string sql) => new SqlCommand(sql, connection).ExecuteScalar()?.ToString() ?? "?";

        var version = Scalar("SELECT @@VERSION").Split('\n')[0].Trim();
        var cpus = Scalar("SELECT cpu_count FROM sys.dm_os_sys_info");
        var memoryGb = double.Parse(Scalar("SELECT physical_memory_kb FROM sys.dm_os_sys_info"), CultureInfo.InvariantCulture) / 1024 / 1024;
        var compat = Scalar("SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()");

        print("## Setup");
        print("");
        print($"- Client machine: {RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}, {Environment.ProcessorCount} logical CPUs, " +
              $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024 / 1024:F0} GB RAM");
        print($"- SQL Server (as seen from inside its container): {version}; {cpus} CPUs and {memoryGb:F1} GB visible; database compatibility level {compat}");
        print($"- Database: {connection.Database}; data as of {asOf:yyyy-MM-dd}");
        foreach (var table in new[] { "Visits", "Invoices", "Containers", "ShippingLines", "YardSlots" })
        {
            print($"- {table}: {int.Parse(Scalar($"SELECT COUNT(*) FROM dbo.{table}"), CultureInfo.InvariantCulture):N0} rows");
        }

        print($"- Method: per procedure and parameter set, 1 discarded run, then {TimedRuns} timed runs on one connection (median and p95 of wall-clock milliseconds); " +
              "one extra run with STATISTICS IO and STATISTICS XML for logical reads and the plan; a hash of the result rows compares baseline and current.");
    }
}
