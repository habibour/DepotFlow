namespace DepotFlow.Domain.Billing;

/// <summary>
/// Each tier bills the days where its range overlaps the billable window [FreeDays + 1, dwellDays].
/// With free 4, tiers 5-10 at 200 and 11+ at 400, a 12 day stay is 6 x 200 + 2 x 400 = 2,000.
/// </summary>
public sealed class TieredStrategy : ITariffStrategy
{
    public ChargeResult Calculate(TariffSnapshot tariff, int dwellDays)
    {
        var windowStart = tariff.FreeDays + 1;
        var lines = new List<ChargeLine>();

        foreach (var tier in tariff.Tiers.OrderBy(t => t.FromDay))
        {
            var from = Math.Max(tier.FromDay, windowStart);
            var to = Math.Min(tier.ToDay ?? dwellDays, dwellDays);
            if (to < from)
            {
                continue;   // this tier is entirely before the window or after the stay
            }

            var days = to - from + 1;
            lines.Add(new ChargeLine(from, to, days, tier.RatePerDay, days * tier.RatePerDay));
        }

        return ChargeResult.From(lines);
    }
}
