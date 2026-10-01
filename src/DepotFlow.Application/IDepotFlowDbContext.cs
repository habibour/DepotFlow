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

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index rejected the write.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
