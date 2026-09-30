using System.Linq.Expressions;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.ShippingLines;

public sealed record ShippingLineDto(int Id, string Code, string Name, bool IsActive, DateTime CreatedAtUtc);

public sealed record CreateShippingLineRequest(string? Code, string? Name);

public sealed record UpdateShippingLineRequest(string? Name, bool IsActive);

internal static class ShippingLineMapping
{
    // An expression (not a method) so EF Core can translate it and select only these columns.
    public static readonly Expression<Func<ShippingLine, ShippingLineDto>> Projection =
        x => new ShippingLineDto(x.Id, x.Code, x.Name, x.IsActive, x.CreatedAtUtc);

    private static readonly Func<ShippingLine, ShippingLineDto> Compiled = Projection.Compile();

    public static ShippingLineDto ToDto(this ShippingLine entity) => Compiled(entity);
}
