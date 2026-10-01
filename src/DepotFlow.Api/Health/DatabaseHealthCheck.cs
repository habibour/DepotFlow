using DepotFlow.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DepotFlow.Api.Health;

/// <summary>/health reports Unhealthy (HTTP 503) when the database cannot be reached within a few seconds.</summary>
public sealed class DatabaseHealthCheck(IDepotFlowDbContext db) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            return await db.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Database did not answer within {Timeout.TotalSeconds:0} seconds.");
        }
    }
}
