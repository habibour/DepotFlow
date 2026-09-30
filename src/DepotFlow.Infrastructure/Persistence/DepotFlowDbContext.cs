using DepotFlow.Application;
using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Infrastructure.Persistence;

public class DepotFlowDbContext(DbContextOptions<DepotFlowDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDepotFlowDbContext
{
    // SQL Server error numbers for "duplicate key" (unique index / unique constraint).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public DbSet<ShippingLine> ShippingLines => Set<ShippingLine>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Visit> Visits => Set<Visit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // Identity tables

        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DepotFlowDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new UniqueConstraintViolationException(ex.InnerException.Message, ex);
        }
    }
}
