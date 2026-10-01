namespace DepotFlow.Domain.Billing;

/// <summary>One rate for every day after the free days: billable days = max(0, dwellDays - FreeDays).</summary>
public sealed class FlatStrategy : ITariffStrategy
{
    public ChargeResult Calculate(TariffSnapshot tariff, int dwellDays)
    {
        if (tariff.Tiers.Count != 1)
        {
            throw new DomainException("A flat tariff must have exactly one tier.");
        }

        var days = Math.Max(0, dwellDays - tariff.FreeDays);
        if (days == 0)
        {
            return ChargeResult.From([]);
        }

        var rate = tariff.Tiers[0].RatePerDay;
        return ChargeResult.From([new ChargeLine(tariff.FreeDays + 1, dwellDays, days, rate, days * rate)]);
    }
}
