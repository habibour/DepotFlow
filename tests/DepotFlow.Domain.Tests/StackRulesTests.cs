using DepotFlow.Domain.Yard;

namespace DepotFlow.Domain.Tests;

// Covers spec S2-02 (stacking rule).
public class StackRulesTests
{
    private static SlotPosition Slot(int tier, int bay = 1) => new("A", 1, bay, tier);

    private static HashSet<SlotPosition> Occupied(params SlotPosition[] slots) => [.. slots];

    [Fact]
    public void Free_tier_1_slot_can_be_used() =>
        Assert.Equal(Placement.Ok, StackRules.Evaluate(Slot(1), Occupied()));

    [Fact]
    public void Tier_2_with_tier_1_free_cannot_be_used() =>
        Assert.Equal(Placement.StackingRuleViolated, StackRules.Evaluate(Slot(2), Occupied()));

    [Fact]
    public void Tier_2_with_tier_1_occupied_can_be_used() =>
        Assert.Equal(Placement.Ok, StackRules.Evaluate(Slot(2), Occupied(Slot(1))));

    [Fact]
    public void Occupied_target_cannot_be_used() =>
        Assert.Equal(Placement.Occupied, StackRules.Evaluate(Slot(1), Occupied(Slot(1))));

    [Fact]
    public void Occupied_is_reported_before_the_stacking_rule() =>
        Assert.Equal(Placement.Occupied, StackRules.Evaluate(Slot(2), Occupied(Slot(2))));

    [Fact]
    public void Tier_3_needs_tier_2_not_just_tier_1() =>
        Assert.Equal(Placement.StackingRuleViolated, StackRules.Evaluate(Slot(3), Occupied(Slot(1))));

    [Fact]
    public void A_different_bay_does_not_count_as_support() =>
        Assert.Equal(Placement.StackingRuleViolated, StackRules.Evaluate(Slot(2, bay: 1), Occupied(Slot(1, bay: 2))));

    [Fact]
    public void CanPlace_is_true_only_for_ok()
    {
        Assert.True(StackRules.CanPlace(Slot(1), Occupied()));
        Assert.False(StackRules.CanPlace(Slot(2), Occupied()));
    }
}
