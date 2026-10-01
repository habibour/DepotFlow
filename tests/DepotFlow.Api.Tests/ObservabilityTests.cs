using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application;
using DepotFlow.Application.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DepotFlow.Api.Tests;

// Covers spec S3-12 (logging and health).
[Collection(ApiCollection.Name)]
public class ObservabilityTests(ApiFactory factory) : ApiTestBase(factory)
{
    private sealed class ThrowingClock : IClock
    {
        public DateTime UtcNow => throw new InvalidOperationException("simulated unexpected failure");
    }

    [Fact]
    public async Task S3_T_HEALTH_1_health_is_200_and_anonymous_when_the_database_is_up()
    {
        var response = await Factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task S3_T_HEALTH_2_health_is_503_when_the_database_is_unreachable()
    {
        // Production environment, so the app starts without touching the (unreachable) database.
        using var broken = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("ConnectionStrings:Default", "Server=127.0.0.1,1;Database=nothing;User Id=sa;Password=x;Connect Timeout=1;TrustServerCertificate=True");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=127.0.0.1,1;Database=nothing;User Id=sa;Password=x;Connect Timeout=1;TrustServerCertificate=True"
            }));
        });

        var response = await broken.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task S3_T_LOG_1_an_unexpected_error_returns_a_trace_id_that_appears_in_the_logs()
    {
        var logs = new CapturingLoggerProvider();
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);   // sign in first: login also needs the clock
        var token = clerk.DefaultRequestHeaders.Authorization!.Parameter!;

        using var failing = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock, ThrowingClock>();
            });
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        var client = failing.CreateClient();
        client.DefaultRequestHeaders.Authorization = clerk.DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync("/api/v1/visits/gate-in", TestData.GateInBody(TestData.NewContainerNumber()));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unexpected_error", problem.RootElement.GetProperty("code").GetString());
        var traceId = problem.RootElement.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrEmpty(traceId));

        // The exception was logged, and the entry carries the same trace id the client received.
        var error = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
        var loggedTraceId = error.Scopes.Single(s => s.StartsWith("TraceId=", StringComparison.Ordinal))["TraceId=".Length..];
        Assert.Contains(loggedTraceId, traceId);

        // And the problem response does not leak the exception details.
        Assert.DoesNotContain("simulated unexpected failure", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(token, string.Join('\n', logs.Entries.Select(e => e.Message)));
    }

    [Fact]
    public async Task S3_T_LOG_2_each_request_is_logged_with_method_path_status_and_duration_and_never_the_token()
    {
        var logs = new CapturingLoggerProvider();
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var token = admin.DefaultRequestHeaders.Authorization!.Parameter!;
        using var logged = Factory.WithWebHostBuilder(builder => builder.ConfigureLogging(l => l.AddProvider(logs)));
        var client = logged.CreateClient();
        client.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;

        await client.GetAsync("/api/v1/shipping-lines?isActive=true");
        await client.GetAsync("/api/v1/visits/999999");

        var lines = logs.Entries.Where(e => e.Category.EndsWith("RequestLoggingMiddleware", StringComparison.Ordinal)).Select(e => e.Message).ToList();
        Assert.Contains(lines, l => l.StartsWith("HTTP GET /api/v1/shipping-lines responded 200 in ", StringComparison.Ordinal) && l.EndsWith(" ms", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("HTTP GET /api/v1/visits/999999 responded 404 in ", StringComparison.Ordinal));
        Assert.All(lines, l => Assert.DoesNotContain("isActive", l));   // the query string is not logged
        Assert.DoesNotContain(token, string.Join('\n', logs.Entries.Select(e => e.Message)));
        Assert.DoesNotContain("Bearer", string.Join('\n', logs.Entries.Select(e => e.Message)));
    }
}
