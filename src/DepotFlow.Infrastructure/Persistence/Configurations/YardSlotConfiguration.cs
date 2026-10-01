using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class YardSlotConfiguration : IEntityTypeConfiguration<YardSlot>
{
    public void Configure(EntityTypeBuilder<YardSlot> builder)
    {
        builder.ToTable("YardSlots");

        builder.Property(x => x.Block).HasColumnType("char(1)").IsRequired();
        builder.Property(x => x.Row).HasColumnType("tinyint");
        builder.Property(x => x.Bay).HasColumnType("tinyint");
        builder.Property(x => x.Tier).HasColumnType("tinyint");

        // Computed and stored by SQL Server, format A-01-03-2 (block, row, bay, tier).
        builder.Property(x => x.Code)
            .HasColumnType("varchar(12)")
            .HasComputedColumnSql(
                "[Block] + '-' + RIGHT('0' + CAST([Row] AS varchar(3)), 2) + '-' + RIGHT('0' + CAST([Bay] AS varchar(3)), 2) + '-' + CAST([Tier] AS varchar(3))",
                stored: true);

        builder.Ignore(x => x.Position);

        builder.HasIndex(x => new { x.Block, x.Row, x.Bay, x.Tier }).IsUnique();
    }
}
