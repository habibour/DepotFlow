using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.Gate;

public sealed record GateInRequest(
    string? ContainerNumber,
    int? SizeFeet,
    int? ShippingLineId,
    string? TruckNumber,
    string? SealNumber,
    string? DamageNotes);

public sealed record GateOutRequest(string? TruckNumber, string? DamageNotes);

public sealed record ShippingLineSummary(int Id, string Code, string Name);

public sealed record VisitDto(
    long Id,
    int ContainerId,
    string ContainerNumber,
    int SizeFeet,
    ShippingLineSummary ShippingLine,
    VisitStatus Status,
    DateTime GateInAtUtc,
    DateTime? GateOutAtUtc,
    string TruckInNumber,
    string? TruckOutNumber,
    string SealNumber,
    int? DwellDays);

internal static class VisitMapping
{
    /// <summary>Expects <see cref="Visit.Container"/> and <see cref="Visit.ShippingLine"/> to be loaded.</summary>
    public static VisitDto ToDto(this Visit visit, int? dwellDays = null) => new(
        visit.Id,
        visit.ContainerId,
        visit.Container.Number,
        visit.Container.SizeFeet,
        new ShippingLineSummary(visit.ShippingLine.Id, visit.ShippingLine.Code, visit.ShippingLine.Name),
        visit.Status,
        visit.GateInAtUtc,
        visit.GateOutAtUtc,
        visit.TruckInNumber,
        visit.TruckOutNumber,
        visit.SealNumber,
        dwellDays);
}
