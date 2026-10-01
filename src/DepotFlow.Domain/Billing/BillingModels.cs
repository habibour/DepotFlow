namespace DepotFlow.Domain.Billing;

public static class StrategyKeys
{
    public const string Tiered = "Tiered";
    public const string Flat = "Flat";
}

/// <param name="FromDay">First billable day of this tier (day 1 is the gate-in day).</param>
/// <param name="ToDay">Last day of this tier; null means open-ended.</param>
public sealed record TariffTierSnapshot(int FromDay, int? ToDay, decimal RatePerDay);

/// <summary>A tariff reduced to the plain values the billing maths needs. Invoices copy these so later tariff edits cannot change them.</summary>
public sealed record TariffSnapshot(string StrategyKey, int FreeDays, string Currency, IReadOnlyList<TariffTierSnapshot> Tiers);

public sealed record ChargeLine(int FromDay, int ToDay, int Days, decimal RatePerDay, decimal Amount);

public sealed record ChargeResult(IReadOnlyList<ChargeLine> Lines, decimal Total)
{
    public static ChargeResult From(IReadOnlyList<ChargeLine> lines) =>
        new(lines, Math.Round(lines.Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero));
}
