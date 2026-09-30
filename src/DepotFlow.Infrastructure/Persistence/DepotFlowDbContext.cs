using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Infrastructure.Persistence;

public class DepotFlowDbContext(DbContextOptions<DepotFlowDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ShippingLine> ShippingLines => Set<ShippingLine>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Visit> Visits => Set<Visit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // Identity tables

        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DepotFlowDbContext).Assembly);
    }
}
