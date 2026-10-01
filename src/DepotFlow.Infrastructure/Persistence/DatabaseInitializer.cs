using DepotFlow.Application.Security;
using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure.Identity;
using DepotFlow.Infrastructure.Yard;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    private static readonly (string Email, string Role)[] Users =
    [
        ("admin@depotflow.local", Roles.Admin),
        ("gate@depotflow.local", Roles.GateClerk),
        ("yard@depotflow.local", Roles.YardPlanner),
        ("billing@depotflow.local", Roles.BillingOfficer)
    ];

    /// <summary>Applies pending migrations and seeds reference data. Intended for Development startup.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepotFlowDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
        await SeedShippingLinesAsync(db, cancellationToken);
        await SeedIdentityAsync(scope.ServiceProvider);
        await SeedYardSlotsAsync(db, scope.ServiceProvider.GetRequiredService<IOptions<YardOptions>>().Value, cancellationToken);
    }

    private static async Task SeedYardSlotsAsync(DepotFlowDbContext db, YardOptions yard, CancellationToken cancellationToken)
    {
        if (await db.YardSlots.AnyAsync(cancellationToken))
        {
            return;
        }

        var slots = from block in yard.Blocks
                    from row in Enumerable.Range(1, yard.Rows)
                    from bay in Enumerable.Range(1, yard.Bays)
                    from tier in Enumerable.Range(1, yard.Tiers)
                    select new YardSlot(block.ToString(), row, bay, tier);

        db.YardSlots.AddRange(slots);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedIdentityAsync(IServiceProvider services)
    {
        var password = services.GetRequiredService<IConfiguration>()["Seed:DefaultPassword"]
            ?? throw new InvalidOperationException("Seed:DefaultPassword is not configured.");

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"create role {role}");
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (email, role) in Users)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            EnsureSucceeded(await userManager.CreateAsync(user, password), $"create user {email}");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"add {email} to {role}");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Seeding failed to {action}: {errors}");
        }
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
