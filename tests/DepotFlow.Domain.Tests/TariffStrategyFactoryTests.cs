using DepotFlow.Domain.Billing;

namespace DepotFlow.Domain.Tests;

// Covers spec S2-06 (factory).
public class TariffStrategyFactoryTests
{
    [Fact]
    public void Tiered_key_returns_the_tiered_strategy() =>
        Assert.IsType<TieredStrategy>(TariffStrategyFactory.For("Tiered"));

    [Fact]
    public void Flat_key_returns_the_flat_strategy() =>
        Assert.IsType<FlatStrategy>(TariffStrategyFactory.For("Flat"));

    [Theory]
    [InlineData("tiered")]
    [InlineData("Weekly")]
    [InlineData("")]
    public void Unknown_key_throws(string key) =>
        Assert.Throws<ArgumentException>(() => TariffStrategyFactory.For(key));
}
