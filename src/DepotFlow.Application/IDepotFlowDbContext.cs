using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application;

/// <summary>What use cases need from the database. Implemented by DepotFlowDbContext in Infrastructure.</summary>
public interface IDepotFlowDbContext
{
    DbSet<ShippingLine> ShippingLines { get; }
    DbSet<Container> Containers { get; }
    DbSet<Visit> Visits { get; }
    DbSet<YardSlot> YardSlots { get; }
    DbSet<Tariff> Tariffs { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Runs a raw SQL query (for example EXEC of a report procedure) and maps the columns onto <typeparamref name="T"/>.
    /// Interpolated values become SQL parameters, never concatenated text.
    /// </summary>
    IQueryable<T> SqlQuery<T>(FormattableString sql);

    /// <summary>True when the database can be reached. Used by the health check.</summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every tracked change. Call after a failed save, before retrying with fresh data.</summary>
    void ResetChanges();

    /// <summary>Starts a transaction so several saves commit or roll back together.</summary>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index rejected the write.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
