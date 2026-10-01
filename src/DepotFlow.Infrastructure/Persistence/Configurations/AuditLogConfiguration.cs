using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.UserEmail).HasMaxLength(256);
        builder.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasColumnType("tinyint");
        builder.Property(x => x.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValues).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.EntityName, x.EntityKey });
        builder.HasIndex(x => x.OccurredAtUtc);
    }
}
