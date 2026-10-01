using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public const string NumberSequence = "InvoiceNumberSeq";

    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t => t.HasCheckConstraint("CK_Invoices_Total", "[Total] >= 0"));

        // INV-2026-0000123: the year, then the next value of a SQL sequence padded to 7 digits.
        // The database generates it, so two requests can never receive the same number.
        builder.Property(x => x.InvoiceNumber)
            .HasColumnType("varchar(20)")
            .HasDefaultValueSql(
                $"'INV-' + CAST(YEAR(SYSUTCDATETIME()) AS varchar(4)) + '-' + RIGHT('0000000' + CAST(NEXT VALUE FOR [{NumberSequence}] AS varchar(10)), 7)");
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.Property(x => x.IssuedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.StrategyKey).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Total).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PaidAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.PaidByUserId).HasMaxLength(450);

        // Two people paying the same invoice at once: the second UPDATE finds Status changed and fails,
        // which the use case reports as invoice_already_paid.
        builder.Property(x => x.Status).HasColumnType("tinyint").IsConcurrencyToken();

        builder.HasOne(x => x.Visit)
            .WithMany()
            .HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ShippingLine>()
            .WithMany()
            .HasForeignKey(x => x.ShippingLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supports the revenue report: a range seek on the issue time that already holds every column the report reads,
        // so it never touches the (wide) clustered rows. Measured in docs/benchmarks.md.
        builder.HasIndex(x => x.IssuedAtUtc)
            .IncludeProperties(x => new { x.ShippingLineId, x.Total, x.Status })
            .HasDatabaseName("IX_Invoices_IssuedAt_Covering");

        // One invoice per visit. This also stops two simultaneous gate-outs from both succeeding.
        builder.HasIndex(x => x.VisitId).IsUnique().HasDatabaseName(IndexNames.InvoiceVisit);
    }
}
