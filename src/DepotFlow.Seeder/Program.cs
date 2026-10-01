using System.Diagnostics;
using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure;
using DepotFlow.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DepotFlow.Seeder;

/// <summary>
///   dotnet run --project src/DepotFlow.Seeder -- --visits 1500000 --seed 42 --reset
///   dotnet run --project src/DepotFlow.Seeder -- --bench
/// Loads a large, realistic dataset into a dedicated database (default DepotFlowBench) for the report benchmarks.
/// </summary>
internal static class Program
{
    // The API's user-secrets id, so the seeder finds the same local connection string without a copy of the password.
    private const string ApiUserSecretsId = "d83805db-385f-47e7-b708-ccd3d43ee1d3";
    private const string DemoPassword = "Demo#DepotFlow1";
    private const int TargetShippingLines = 25;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static void Log(string message) => Console.WriteLine($"[{Clock.Elapsed:hh\\:mm\\:ss}] {message}");

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = SeederOptions.Parse(args);
            var connectionString = ResolveConnectionString(options, out var devDatabase);
            return options.Bench
                ? await Benchmark.RunAsync(connectionString, options.AsOf, options.Only, Log)
                : await SeedAsync(options, connectionString, devDatabase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    private static string ResolveConnectionString(SeederOptions options, out string devDatabase)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var raw = options.Connection ?? config["ConnectionStrings:Default"]
            ?? throw new InvalidOperationException(
                "No connection string found. Set ConnectionStrings__Default, pass --connection, or configure the API's user-secrets.");

        var builder = new SqlConnectionStringBuilder(raw);
        devDatabase = builder.InitialCatalog;

        // Safety: never touch anything that is not on this machine.
        var host = builder.DataSource.Split(',')[0].Replace("tcp:", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (host is not ("localhost" or "127.0.0.1" or "."))
        {
            throw new InvalidOperationException($"The seeder only runs against a local SQL Server, not '{host}'.");
        }

        builder.InitialCatalog = options.Database;
        builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }

    private static async Task<int> SeedAsync(SeederOptions options, string connectionString, string devDatabase)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        Log($"Seeding database '{database}' with {options.Visits:N0} visits (seed {options.Seed}, as of {options.AsOf:yyyy-MM-dd}){(options.Reset ? ", resetting first" : "")}");

        // --reset drops the whole database, so it is only allowed on a dedicated benchmark database.
        if (options.Reset && (!database.Contains("Bench", StringComparison.OrdinalIgnoreCase)
                              || database.Equals(devDatabase, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"--reset drops the database. Refusing: '{database}' must contain 'Bench' and must not be your development database ('{devDatabase}').");
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Seed:DefaultPassword"] = DemoPassword,
            ["Seed:DemoTariffs"] = "false"   // tariffs for all 25 lines are created below
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        await using var provider = services.BuildServiceProvider();

        // ---- 1. database ------------------------------------------------------------------------------------
        if (options.Reset)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>().Database.EnsureDeletedAsync();
            Log("Dropped the database.");
        }

        await DatabaseInitializer.InitializeAsync(provider);   // migrate, users, the first shipping lines, yard slots
        Log("Database is migrated; users and yard slots exist.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var existing = await ScalarAsync(connection, "SELECT (SELECT COUNT(*) FROM dbo.Visits) + (SELECT COUNT(*) FROM dbo.Containers) + (SELECT COUNT(*) FROM dbo.Invoices)");
        if (existing > 0)
        {
            throw new InvalidOperationException(
                $"'{database}' already holds {existing:N0} container, visit or invoice rows. Run again with --reset to start over.");
        }

        // ---- 2. shipping lines and tariffs --------------------------------------------------------------------
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();
            await TopUpShippingLinesAsync(db);
            await DemoTariffSeeder.SeedAsync(db, CancellationToken.None);   // a tariff for every line and both sizes
        }

        var context = await BuildLoadContextAsync(provider, options);
        var lineIds = await QueryAsync(connection, "SELECT Id FROM dbo.ShippingLines ORDER BY Id", r => r.GetInt32(0));
        var slots = await QueryAsync(connection, "SELECT Id, Block, [Row], Bay, Tier FROM dbo.YardSlots",
            r => new SlotInfo(r.GetInt32(0), r.GetString(1), r.GetByte(2), r.GetByte(3), r.GetByte(4)));
        Log($"{lineIds.Count} shipping lines, {context.Tariffs.Count} active tariffs, {slots.Count} yard slots.");

        // ---- 3. generate -------------------------------------------------------------------------------------
        Log("Generating data in memory...");
        var data = DataGenerator.Generate(options, lineIds, slots, Log);

        // ---- 4. load -----------------------------------------------------------------------------------------
        Log("Loading with SqlBulkCopy (batches of 50,000)...");
        var windowStart = data.AsOfUtc.AddMonths(-36);
        await BulkLoader.LoadContainersAsync(connection, data, windowStart, Log);
        await BulkLoader.LoadVisitsAsync(connection, data, context, Log);
        var invoices = await BulkLoader.LoadInvoicesAsync(connection, data, context, Log);

        Log("Reseeding identity counters and updating statistics...");
        await ExecuteAsync(connection, $"DBCC CHECKIDENT ('dbo.Containers', RESEED, {data.ContainerNumbers.Length}) WITH NO_INFOMSGS");
        await ExecuteAsync(connection, $"DBCC CHECKIDENT ('dbo.Visits', RESEED, {data.Visits.Length}) WITH NO_INFOMSGS");
        await ExecuteAsync(connection, $"DBCC CHECKIDENT ('dbo.Invoices', RESEED, {invoices}) WITH NO_INFOMSGS");
        foreach (var table in new[] { "Containers", "Visits", "Invoices" })
        {
            await ExecuteAsync(connection, $"UPDATE STATISTICS dbo.{table}");
        }

        var loadTime = Clock.Elapsed;
        Log($"Load finished in {loadTime:hh\\:mm\\:ss}. Checking the result...");

        // ---- 5. verify ---------------------------------------------------------------------------------------
        var ok = await SelfChecks.RunAsync(connection, options, context.Tariffs.ToDictionary(t => (t.Key.ShippingLineId, t.Key.SizeFeet), t => t.Value), Log);

        Log($"Not seeded (bulk loading bypasses the audit interceptor): InvoiceLines and AuditLogs.");
        Log($"Total time {Clock.Elapsed:hh\\:mm\\:ss} (load {loadTime:hh\\:mm\\:ss}). Self-checks {(ok ? "PASSED" : "FAILED")}.");
        return ok ? 0 : 1;
    }

    private static async Task TopUpShippingLinesAsync(DepotFlowDbContext db)
    {
        (string Code, string Name)[] more =
        [
            ("ZIMU", "ZIM"), ("YMLU", "Yang Ming"), ("OOLU", "OOCL"), ("PILU", "PIL"), ("ANNU", "ANL"),
            ("WHLU", "Wan Hai Lines"), ("MATU", "Matson"), ("ARKU", "Arkas"), ("SINU", "Sinotrans"),
            ("XPRU", "X-Press Feeders"), ("SWRU", "Swire Shipping"), ("CNCU", "CNC Line"), ("TWLU", "Transworld"),
            ("SAFU", "Safmarine"), ("SEAU", "SeaLand"), ("SITU", "SITC"), ("GLDU", "Gold Star Line"),
            ("APLU", "APL"), ("TGHU", "Tianjin Gang"), ("CAIU", "Cai Container")
        ];

        var have = await db.ShippingLines.CountAsync();
        var existingCodes = (await db.ShippingLines.Select(l => l.Code).ToListAsync()).ToHashSet();
        var now = DateTime.UtcNow;   // seeding is plumbing, not business logic

        foreach (var (code, name) in more.Where(m => !existingCodes.Contains(m.Code)))
        {
            if (have >= TargetShippingLines)
            {
                break;
            }

            db.ShippingLines.Add(new ShippingLine(code, name, now));
            have++;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<LoadContext> BuildLoadContextAsync(IServiceProvider provider, SeederOptions options)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        var users = await db.Users
            .Where(u => u.Email == "gate@depotflow.local" || u.Email == "billing@depotflow.local")
            .ToDictionaryAsync(u => u.Email!, u => u.Id);

        var tariffs = (await db.Tariffs.AsNoTracking().Include(t => t.Tiers).Where(t => t.IsActive).ToListAsync())
            .ToDictionary(t => (t.ShippingLineId, t.SizeFeet), t => t.ToSnapshot());

        return new LoadContext(users["gate@depotflow.local"], users["billing@depotflow.local"], tariffs, options.Seed);
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<T>> QueryAsync<T>(SqlConnection connection, string sql, Func<SqlDataReader, T> map)
    {
        var result = new List<T>();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(map(reader));
        }

        return result;
    }
}
