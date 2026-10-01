using DepotFlow.Domain.Billing;

namespace DepotFlow.Domain.Entities;

public class Tariff
{
    public const string DefaultCurrency = "BDT";

    private Tariff() { }   // for EF Core

    public Tariff(
        int shippingLineId,
        int sizeFeet,
        string strategyKey,
        int freeDays,
        IEnumerable<TariffTier> tiers,
        DateTime createdAtUtc,
        string currency = DefaultCurrency)
    {
        ShippingLineId = shippingLineId;
        SizeFeet = sizeFeet;
        StrategyKey = strategyKey;
        FreeDays = freeDays;
        Currency = currency;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        Tiers = tiers.ToList();
    }

    public int Id { get; private set; }
    public int ShippingLineId { get; private set; }
    public int SizeFeet { get; private set; }
    public string StrategyKey { get; private set; } = null!;
    public int FreeDays { get; private set; }
    public string Currency { get; private set; } = DefaultCurrency;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public List<TariffTier> Tiers { get; private set; } = [];

    public void Deactivate() => IsActive = false;

    public TariffSnapshot ToSnapshot() => new(
        StrategyKey,
        FreeDays,
        Currency,
        Tiers.OrderBy(t => t.FromDay).Select(t => new TariffTierSnapshot(t.FromDay, t.ToDay, t.RatePerDay)).ToList());

    /// <summary>Checks the tier rules (spec 8.1). Returns null when valid, otherwise a message naming the first rule broken.</summary>
    public string? Validate()
    {
        if (SizeFeet is not (20 or 40))
        {
            return "Size must be 20 or 40 feet.";
        }

        if (StrategyKey is not (StrategyKeys.Tiered or StrategyKeys.Flat))
        {
            return $"Strategy must be '{StrategyKeys.Tiered}' or '{StrategyKeys.Flat}'.";
        }

        if (FreeDays < 0)
        {
            return "Free days cannot be negative.";
        }

        if (Tiers.Count == 0)
        {
            return "At least one tier is required.";
        }

        if (StrategyKey == StrategyKeys.Flat && Tiers.Count != 1)
        {
            return "A flat tariff must have exactly one tier.";
        }

        if (Tiers.Any(t => t.RatePerDay <= 0))
        {
            return "Every tier rate must be greater than zero.";
        }

        var tiers = Tiers.OrderBy(t => t.FromDay).ToList();

        if (tiers[0].FromDay != FreeDays + 1)
        {
            return $"The first tier must start at day {FreeDays + 1} (free days + 1).";
        }

        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            var isLast = i == tiers.Count - 1;

            if (tier.ToDay is null && !isLast)
            {
                return "Only the last tier can be open-ended.";
            }

            if (tier.ToDay is null)
            {
                continue;
            }

            if (tier.ToDay < tier.FromDay)
            {
                return $"Tier starting at day {tier.FromDay} ends before it starts.";
            }

            if (isLast)
            {
                return "The last tier must be open-ended (toDay empty).";
            }

            var next = tiers[i + 1];
            if (next.FromDay <= tier.ToDay)
            {
                return $"Tiers must not overlap: day {next.FromDay} is covered twice.";
            }

            if (next.FromDay > tier.ToDay + 1)
            {
                return $"Tiers must not leave gaps: days {tier.ToDay + 1} to {next.FromDay - 1} are not covered.";
            }
        }

        return null;
    }
}
