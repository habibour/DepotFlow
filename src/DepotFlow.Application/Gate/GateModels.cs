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

public sealed record YardSlotSummary(int Id, string Code);

public sealed record RelocateRequest(int? TargetSlotId);

public sealed record InvoiceSummaryDto(long Id, string InvoiceNumber, decimal Total, string Currency, InvoiceStatus Status);

public sealed record VisitDto(
    long Id,
    int ContainerId,
    string ContainerNumber,
    int SizeFeet,
    ShippingLineSummary ShippingLine,
    YardSlotSummary? YardSlot,
    VisitStatus Status,
    DateTime GateInAtUtc,
    DateTime? GateOutAtUtc,
    string TruckInNumber,
    string? TruckOutNumber,
    string SealNumber,
    int? DwellDays,
    InvoiceSummaryDto? Invoice);

internal static class VisitMapping
{
    /// <summary>Expects <see cref="Visit.Container"/>, <see cref="Visit.ShippingLine"/> and (if placed) <see cref="Visit.YardSlot"/> to be loaded.</summary>
    public static VisitDto ToDto(this Visit visit, int? dwellDays = null, Invoice? invoice = null) => new(
        visit.Id,
        visit.ContainerId,
        visit.Container.Number,
        visit.Container.SizeFeet,
        new ShippingLineSummary(visit.ShippingLine.Id, visit.ShippingLine.Code, visit.ShippingLine.Name),
        visit.YardSlot is null ? null : new YardSlotSummary(visit.YardSlot.Id, visit.YardSlot.Code),
        visit.Status,
        visit.GateInAtUtc,
        visit.GateOutAtUtc,
        visit.TruckInNumber,
        visit.TruckOutNumber,
        visit.SealNumber,
        dwellDays,
        invoice is null ? null : new InvoiceSummaryDto(invoice.Id, invoice.InvoiceNumber, invoice.Total, invoice.Currency, invoice.Status));
}
