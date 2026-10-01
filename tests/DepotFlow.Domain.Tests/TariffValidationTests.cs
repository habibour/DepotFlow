using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Domain.Tests;

// Covers spec S2-05 (tier rules, Tariff.Validate).
public class TariffValidationTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Tariff Make(string strategy, int freeDays, params (int From, int? To, decimal Rate)[] tiers) =>
        new(1, 20, strategy, freeDays, tiers.Select(t => new TariffTier(t.From, t.To, t.Rate)), Now);

    [Fact]
    public void Valid_tiered_tariff_has_no_error() =>
        Assert.Null(Make(StrategyKeys.Tiered, 4, (5, 10, 200m), (11, null, 400m)).Validate());

    [Fact]
    public void Valid_single_open_tier_tiered_tariff_has_no_error() =>
        Assert.Null(Make(StrategyKeys.Tiered, 0, (1, null, 100m)).Validate());

    [Fact]
    public void Valid_flat_tariff_has_no_error() =>
        Assert.Null(Make(StrategyKeys.Flat, 3, (4, null, 150m)).Validate());

    [Fact]
    public void Tiers_given_out_of_order_are_still_validated_in_day_order() =>
        Assert.Null(Make(StrategyKeys.Tiered, 4, (11, null, 400m), (5, 10, 200m)).Validate());

    [Fact]
    public void Gap_between_tiers_is_rejected() =>
        Assert.Contains("gaps", Make(StrategyKeys.Tiered, 4, (5, 10, 200m), (12, null, 400m)).Validate());

    [Fact]
    public void Overlapping_tiers_are_rejected() =>
        Assert.Contains("overlap", Make(StrategyKeys.Tiered, 4, (5, 10, 200m), (10, null, 400m)).Validate());

    [Fact]
    public void First_tier_not_at_free_days_plus_one_is_rejected() =>
        Assert.Contains("must start at day 5", Make(StrategyKeys.Tiered, 4, (6, null, 200m)).Validate());

    [Fact]
    public void Last_tier_that_is_not_open_ended_is_rejected() =>
        Assert.Contains("open-ended", Make(StrategyKeys.Tiered, 4, (5, 10, 200m), (11, 20, 400m)).Validate());

    [Fact]
    public void Open_ended_tier_that_is_not_last_is_rejected() =>
        Assert.Contains("Only the last", Make(StrategyKeys.Tiered, 4, (5, null, 200m), (11, null, 400m)).Validate());

    [Fact]
    public void Flat_with_two_tiers_is_rejected() =>
        Assert.Contains("exactly one", Make(StrategyKeys.Flat, 4, (5, 10, 200m), (11, null, 400m)).Validate());

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_rate_is_rejected(double rate) =>
        Assert.Contains("greater than zero", Make(StrategyKeys.Tiered, 0, (1, null, (decimal)rate)).Validate());

    [Fact]
    public void No_tiers_is_rejected() =>
        Assert.Contains("At least one", Make(StrategyKeys.Tiered, 0).Validate());

    [Fact]
    public void Negative_free_days_is_rejected() =>
        Assert.Contains("negative", Make(StrategyKeys.Tiered, -1, (0, null, 100m)).Validate());

    [Fact]
    public void Unknown_strategy_is_rejected() =>
        Assert.Contains("Strategy must be", Make("Weekly", 0, (1, null, 100m)).Validate());

    [Fact]
    public void Snapshot_copies_the_values_in_day_order()
    {
        var snapshot = Make(StrategyKeys.Tiered, 4, (11, null, 400m), (5, 10, 200m)).ToSnapshot();

        Assert.Equal(4, snapshot.FreeDays);
        Assert.Equal([5, 11], snapshot.Tiers.Select(t => t.FromDay));
    }
}
