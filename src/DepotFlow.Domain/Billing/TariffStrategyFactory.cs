namespace DepotFlow.Domain.Billing;

public static class TariffStrategyFactory
{
    private static readonly TieredStrategy Tiered = new();
    private static readonly FlatStrategy Flat = new();

    /// <exception cref="ArgumentException">The key is unknown. That is a programming error, not a user error.</exception>
    public static ITariffStrategy For(string strategyKey) => strategyKey switch
    {
        StrategyKeys.Tiered => Tiered,
        StrategyKeys.Flat => Flat,
        _ => throw new ArgumentException($"Unknown tariff strategy '{strategyKey}'.", nameof(strategyKey))
    };
}
