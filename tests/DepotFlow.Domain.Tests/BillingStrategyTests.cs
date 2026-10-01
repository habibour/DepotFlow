using DepotFlow.Domain;
using DepotFlow.Domain.Billing;

namespace DepotFlow.Domain.Tests;

// Covers spec S2-06 and the billing table in spec 9.1.
public class BillingStrategyTests
{
    private static TariffSnapshot Tiered(int freeDays, params (int From, int? To, decimal Rate)[] tiers) =>
        new(StrategyKeys.Tiered, freeDays, "BDT", tiers.Select(t => new TariffTierSnapshot(t.From, t.To, t.Rate)).ToList());

    private static TariffSnapshot Flat(int freeDays, decimal rate) =>
        new(StrategyKeys.Flat, freeDays, "BDT", [new TariffTierSnapshot(freeDays + 1, null, rate)]);

    // free 4; days 5-10 at 200; day 11+ at 400
    private static readonly TariffSnapshot Standard = Tiered(4, (5, 10, 200m), (11, null, 400m));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 200)]
    [InlineData(10, 1200)]
    [InlineData(11, 1600)]
    [InlineData(12, 2000)]
    [InlineData(30, 9200)]
    public void Tiered_strategy_matches_the_spec_table(int dwellDays, decimal expectedTotal)
    {
        var result = new TieredStrategy().Calculate(Standard, dwellDays);

        Assert.Equal(expectedTotal, result.Total);
    }

    [Fact]
    public void Tiered_single_open_tier_with_no_free_days()
    {
        var result = new TieredStrategy().Calculate(Tiered(0, (1, null, 100m)), 3);

        Assert.Equal(300m, result.Total);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 150)]
    [InlineData(10, 1050)]
    public void Flat_strategy_matches_the_spec_table(int dwellDays, decimal expectedTotal)
    {
        var result = new FlatStrategy().Calculate(Flat(3, 150m), dwellDays);

        Assert.Equal(expectedTotal, result.Total);
    }

    [Fact]
    public void Tiered_12_days_has_two_lines_with_the_right_ranges()
    {
        var result = new TieredStrategy().Calculate(Standard, 12);

        Assert.Equal(
            [new ChargeLine(5, 10, 6, 200m, 1200m), new ChargeLine(11, 12, 2, 400m, 800m)],
            result.Lines);
    }

    [Fact]
    public void Tiered_stay_inside_free_days_has_no_lines()
    {
        Assert.Empty(new TieredStrategy().Calculate(Standard, 4).Lines);
    }

    [Fact]
    public void Flat_line_covers_the_days_after_free_days()
    {
        var result = new FlatStrategy().Calculate(Flat(3, 150m), 10);

        Assert.Equal([new ChargeLine(4, 10, 7, 150m, 1050m)], result.Lines);
    }

    [Fact]
    public void Flat_with_two_tiers_is_a_domain_error()
    {
        var bad = Tiered(0, (1, 5, 10m), (6, null, 20m)) with { StrategyKey = StrategyKeys.Flat };

        Assert.Throws<DomainException>(() => new FlatStrategy().Calculate(bad, 3));
    }

    [Fact]
    public void Totals_are_rounded_to_two_decimals_away_from_zero()
    {
        // 1 day at 0.125 = 0.125, which rounds to 0.13 (not banker's rounding's 0.12).
        var result = new FlatStrategy().Calculate(Flat(0, 0.125m), 1);

        Assert.Equal(0.13m, result.Total);
    }
}
