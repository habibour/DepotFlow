using System.Linq.Expressions;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.Tariffs;

public sealed record TariffTierDto(int FromDay, int? ToDay, decimal RatePerDay);

public sealed record TariffDto(
    int Id,
    int ShippingLineId,
    int SizeFeet,
    string StrategyKey,
    int FreeDays,
    string Currency,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<TariffTierDto> Tiers);

public sealed record TierRequest(int? FromDay, int? ToDay, decimal? RatePerDay);

public sealed record CreateTariffRequest(int? SizeFeet, string? StrategyKey, int? FreeDays, IReadOnlyList<TierRequest>? Tiers);

public sealed record UpdateTariffRequest(bool? IsActive);

internal static class TariffMapping
{
    // An expression so EF Core can translate it into one query that also loads the tiers.
    public static readonly Expression<Func<Tariff, TariffDto>> Projection = t => new TariffDto(
        t.Id, t.ShippingLineId, t.SizeFeet, t.StrategyKey, t.FreeDays, t.Currency, t.IsActive, t.CreatedAtUtc,
        t.Tiers.OrderBy(x => x.FromDay).Select(x => new TariffTierDto(x.FromDay, x.ToDay, x.RatePerDay)).ToList());

    private static readonly Func<Tariff, TariffDto> Compiled = Projection.Compile();

    public static TariffDto ToDto(this Tariff tariff) => Compiled(tariff);
}
