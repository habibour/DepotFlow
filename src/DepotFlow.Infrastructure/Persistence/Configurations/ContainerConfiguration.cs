using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepotFlow.Infrastructure.Persistence.Configurations;

public class ContainerConfiguration : IEntityTypeConfiguration<Container>
{
    public void Configure(EntityTypeBuilder<Container> builder)
    {
        builder.ToTable("Containers", t =>
            t.HasCheckConstraint("CK_Containers_SizeFeet", "[SizeFeet] IN (20, 40)"));

        builder.Property(x => x.Number).HasColumnType("char(11)").IsRequired();
        builder.Property(x => x.SizeFeet).HasColumnType("tinyint");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.Number).IsUnique();

        builder.HasMany(x => x.Visits)
            .WithOne(x => x.Container)
            .HasForeignKey(x => x.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
