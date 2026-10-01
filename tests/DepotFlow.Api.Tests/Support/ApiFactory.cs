using System.Net.Http.Headers;
using System.Net.Http.Json;
using DepotFlow.Application.Auth;
using DepotFlow.Application.Security;
using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace DepotFlow.Api.Tests.Support;

/// <summary>
/// Starts one real SQL Server in a container for the whole test run, then hosts the real API in memory against it.
/// In-memory EF providers do not enforce the unique and check constraints this project relies on.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Pinned tag: the same image docker-compose.yml uses.
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
    private const string TestDatabase = "DepotFlowTests";
    public const string TestPassword = "Test#Passw0rd1";

    private static readonly Dictionary<string, string> Emails = new()
    {
        [Roles.Admin] = "admin@depotflow.local",
        [Roles.GateClerk] = "gate@depotflow.local",
        [Roles.YardPlanner] = "yard@depotflow.local",
        [Roles.BillingOfficer] = "billing@depotflow.local"
    };

    private readonly MsSqlContainer _sql = new MsSqlBuilder(SqlImage).Build();

    public Task InitializeAsync() => _sql.StartAsync();

    public new async Task DisposeAsync()
    {
        await _sql.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");   // Development applies migrations and seeds data at startup

        var connection = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = TestDatabase };

        // Added last, so these win over appsettings and the developer's user-secrets.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connection.ConnectionString,
            ["Jwt:Key"] = "integration-tests-signing-key-0123456789-abcdef",
            ["Seed:DefaultPassword"] = TestPassword,
            ["Seed:DemoTariffs"] = "false"   // tests create the tariffs they need
        }));
    }

    /// <summary>Removes visits, containers and tariffs. Shipping lines, users and yard slots stay: they are seeded once at startup.</summary>
    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        // Guard: never delete data anywhere except the throwaway test database.
        if (new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog != TestDatabase)
        {
            throw new InvalidOperationException("Refusing to reset: the tests are not connected to the test database.");
        }

        await db.Visits.ExecuteDeleteAsync();
        await db.Containers.ExecuteDeleteAsync();
        await db.Tariffs.ExecuteDeleteAsync();   // tiers go with them (cascade)
    }

    /// <summary>
    /// Inserts the standard tiered tariff (free 4 days; days 5-10 at 200; day 11 on at 400 for 20 ft, double for 40 ft)
    /// directly into the database, for tests that need a tariff to exist but are not about tariffs.
    /// </summary>
    public async Task SeedStandardTariffsAsync(params int[] shippingLineIds)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        foreach (var lineId in shippingLineIds)
        {
            foreach (var size in new[] { 20, 40 })
            {
                var rate = size == 20 ? 200m : 400m;
                db.Tariffs.Add(new Tariff(lineId, size, StrategyKeys.Tiered, 4,
                    [new TariffTier(5, 10, rate), new TariffTier(11, null, rate * 2)], DateTime.UtcNow));
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>An HttpClient already signed in as the seeded user for <paramref name="role"/>.</summary>
    public async Task<HttpClient> CreateClientAsync(string role)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Emails[role], password = TestPassword });
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
}
