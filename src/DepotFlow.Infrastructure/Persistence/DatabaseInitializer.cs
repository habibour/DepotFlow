using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DepotFlow.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private static readonly (string Code, string Name)[] ShippingLines =
    [
        ("MAEU", "Maersk"),
        ("MSCU", "MSC"),
        ("CMDU", "CMA CGM"),
        ("ONEY", "Ocean Network Express"),
        ("HLCU", "Hapag-Lloyd"),
        ("EGLV", "Evergreen"),
        ("COSU", "COSCO"),
        ("HDMU", "Hyundai Merchant Marine")
    ];

    /// <summary>Applies pending migrations and seeds reference data. Intended for Development startup.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
        await SeedShippingLinesAsync(db, cancellationToken);
    }

    private static async Task SeedShippingLinesAsync(DepotFlowDbContext db, CancellationToken cancellationToken)
    {
        if (await db.ShippingLines.AnyAsync(cancellationToken))
        {
            return;
        }

        // Seeding is startup plumbing, not business logic, so it reads the clock directly.
        var now = DateTime.UtcNow;
        db.ShippingLines.AddRange(ShippingLines.Select(x => new ShippingLine(x.Code, x.Name, now)));
        await db.SaveChangesAsync(cancellationToken);
    }
}
