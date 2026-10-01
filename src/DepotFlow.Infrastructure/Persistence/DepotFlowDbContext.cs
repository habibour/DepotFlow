using DepotFlow.Application;
using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using DepotFlow.Infrastructure.Identity;
using DepotFlow.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DepotFlow.Infrastructure.Persistence;

public partial class DepotFlowDbContext(DbContextOptions<DepotFlowDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDepotFlowDbContext
{
    // SQL Server error numbers for "duplicate key" (unique index / unique constraint).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    // "... with unique index 'UX_Visits_Slot_Active'" (2601) or "Violation of UNIQUE KEY constraint 'X'" (2627)
    [GeneratedRegex("(?:unique index|constraint) '([^']+)'")]
    private static partial Regex ConstraintNamePattern();

    public DbSet<ShippingLine> ShippingLines => Set<ShippingLine>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<YardSlot> YardSlots => Set<YardSlot>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // Identity tables

        modelBuilder.HasSequence<long>(InvoiceConfiguration.NumberSequence);   // invoice numbers

        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DepotFlowDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQL Server hands back datetime2 with Kind=Unspecified, which serialises without a "Z".
        // Every date in this system is UTC, so mark them as UTC when reading.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    public void ResetChanges() => ChangeTracker.Clear();

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Database.CanConnectAsync(cancellationToken);

    public IQueryable<T> SqlQuery<T>(FormattableString sql) => Database.SqlQuery<T>(sql);

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EfTransactionScope(await Database.BeginTransactionAsync(cancellationToken));

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new UniqueConstraintViolationException(
                ex.InnerException.Message, ConstraintNamePattern().Match(ex.InnerException.Message) is { Success: true } m ? m.Groups[1].Value : null, ex);
        }
    }
}

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
