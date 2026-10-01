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

    /// <summary>Forgets every tracked change. Call after a failed save, before retrying with fresh data.</summary>
    void ResetChanges();

    /// <summary>Starts a transaction so several saves commit or roll back together.</summary>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index rejected the write.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
