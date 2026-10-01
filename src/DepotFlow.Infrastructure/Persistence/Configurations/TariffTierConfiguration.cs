using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class TariffTierConfiguration : IEntityTypeConfiguration<TariffTier>
{
    public void Configure(EntityTypeBuilder<TariffTier> builder)
    {
        builder.ToTable("TariffTiers", t => t.HasCheckConstraint("CK_TariffTiers_Rate", "[RatePerDay] > 0"));

        builder.Property(x => x.RatePerDay).HasColumnType("decimal(18,2)");
    }
}
