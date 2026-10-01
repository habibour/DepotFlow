using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Infrastructure.Persistence;

/// <summary>
/// Gives every seeded shipping line a tariff for both container sizes, so a Swagger walkthrough works straight away.
/// Switched by the Seed:DemoTariffs setting (on in Development, off in tests, which create their own tariffs).
/// </summary>
public static class DemoTariffSeeder
{
    public const string SettingName = "Seed:DemoTariffs";

    public static async Task SeedAsync(DepotFlowDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Tariffs.AnyAsync(cancellationToken))
        {
            return;
        }

        var lineIds = await db.ShippingLines.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;   // seeding is startup plumbing, not business logic

        for (var i = 0; i < lineIds.Count; i++)
        {
            foreach (var size in new[] { 20, 40 })
            {
                db.Tariffs.Add(i % 2 == 0 ? Tiered(lineIds[i], size, now) : Flat(lineIds[i], size, now));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Free 4 days, then days 5-10 at the base rate and day 11 onwards at double.
    private static Tariff Tiered(int lineId, int size, DateTime now)
    {
        var rate = size == 20 ? 200m : 400m;
        return new Tariff(lineId, size, StrategyKeys.Tiered, 4,
            [new TariffTier(5, 10, rate), new TariffTier(11, null, rate * 2)], now);
    }

    // Free 3 days, then one flat daily rate.
    private static Tariff Flat(int lineId, int size, DateTime now) =>
        new(lineId, size, StrategyKeys.Flat, 3, [new TariffTier(4, null, size == 20 ? 150m : 300m)], now);
}
