using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("Visits", t => t.HasCheckConstraint(
            "CK_Visits_Release",
            "([Status] <> 2 OR [GateOutAtUtc] IS NOT NULL) AND ([GateOutAtUtc] IS NULL OR [GateOutAtUtc] >= [GateInAtUtc])"));

        builder.Property(x => x.Status).HasColumnType("tinyint");
        builder.Property(x => x.GateInAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.GateOutAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.TruckInNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TruckOutNumber).HasMaxLength(30);
        builder.Property(x => x.SealNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DamageNotesIn).HasMaxLength(500);
        builder.Property(x => x.DamageNotesOut).HasMaxLength(500);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ClosedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.ShippingLine)
            .WithMany()
            .HasForeignKey(x => x.ShippingLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.YardSlot)
            .WithMany()
            .HasForeignKey(x => x.YardSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one active visit may sit in a slot, even under concurrent requests.
        builder.HasIndex(x => x.YardSlotId)
            .IsUnique()
            .HasFilter("[Status] = 1 AND [YardSlotId] IS NOT NULL")
            .HasDatabaseName(IndexNames.VisitSlotActive);

        // At most one active (InYard = 1) visit per container, even under concurrent requests.
        builder.HasIndex(x => x.ContainerId)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName(IndexNames.VisitContainerActive);

        // Visit history lookup: newest first for one container.
        builder.HasIndex(x => new { x.ContainerId, x.GateInAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Visits_Container_GateIn");
    }
}
