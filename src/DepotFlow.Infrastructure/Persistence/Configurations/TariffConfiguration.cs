using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class TariffConfiguration : IEntityTypeConfiguration<Tariff>
{
    public void Configure(EntityTypeBuilder<Tariff> builder)
    {
        builder.ToTable("Tariffs", t =>
        {
            t.HasCheckConstraint("CK_Tariffs_SizeFeet", "[SizeFeet] IN (20, 40)");
            t.HasCheckConstraint("CK_Tariffs_FreeDays", "[FreeDays] >= 0");
        });

        builder.Property(x => x.SizeFeet).HasColumnType("tinyint");
        builder.Property(x => x.StrategyKey).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").HasDefaultValue(Tariff.DefaultCurrency);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");

        builder.HasOne<ShippingLine>()
            .WithMany()
            .HasForeignKey(x => x.ShippingLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Tiers)
            .WithOne()
            .HasForeignKey(x => x.TariffId)
            .OnDelete(DeleteBehavior.Cascade);

        // At most one active tariff per shipping line and container size.
        builder.HasIndex(x => new { x.ShippingLineId, x.SizeFeet })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_Tariffs_Line_Size_Active");
    }
}
