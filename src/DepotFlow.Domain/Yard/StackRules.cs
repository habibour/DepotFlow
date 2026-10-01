namespace DepotFlow.Domain.Yard;

/// <summary>Where a slot is in the yard: block, row, bay, and tier (1 is the ground level).</summary>
public readonly record struct SlotPosition(string Block, int Row, int Bay, int Tier)
{
    public SlotPosition Below => this with { Tier = Tier - 1 };
}

public enum Placement
{
    Ok,
    Occupied,
    StackingRuleViolated
}

/// <summary>
/// A container can be placed in a free slot at tier 1, or at tier N above 1 only when the slot at tier N-1
/// in the same block, row and bay is occupied. This is checked when placing; the system does not model
/// re-handling moves when a lower container leaves (see the README).
/// </summary>
public static class StackRules
{
    public static Placement Evaluate(SlotPosition target, IReadOnlySet<SlotPosition> occupied)
    {
        if (occupied.Contains(target))
        {
            return Placement.Occupied;
        }

        if (target.Tier > 1 && !occupied.Contains(target.Below))
        {
            return Placement.StackingRuleViolated;
        }

        return Placement.Ok;
    }

    public static bool CanPlace(SlotPosition target, IReadOnlySet<SlotPosition> occupied) =>
        Evaluate(target, occupied) == Placement.Ok;
}
