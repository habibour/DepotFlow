using DepotFlow.Application.Gate;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.Visits;

public sealed record VisitListItemDto(
    long Id,
    int ContainerId,
    string ContainerNumber,
    int SizeFeet,
    string ShippingLineCode,
    string? SlotCode,
    VisitStatus Status,
    DateTime GateInAtUtc,
    DateTime? GateOutAtUtc,
    int? DwellDays);   // days so far, for visits still in the yard

public sealed record VisitDetailDto(
    long Id,
    int ContainerId,
    string ContainerNumber,
    int SizeFeet,
    string ShippingLineCode,
    string? SlotCode,
    VisitStatus Status,
    DateTime GateInAtUtc,
    DateTime? GateOutAtUtc,
    int? DwellDays,
    InvoiceSummaryDto? Invoice);   // present once the visit has been released

/// <summary>The raw row from the database, before the clock-dependent dwell days are added.</summary>
internal sealed record VisitRow(
    long Id, int ContainerId, string ContainerNumber, int SizeFeet, string ShippingLineCode, string? SlotCode,
    VisitStatus Status, DateTime GateInAtUtc, DateTime? GateOutAtUtc);

internal static class VisitQueryMapping
{
    /// <summary>Calendar days in the yard so far; guards against a clock that is behind the gate-in time.</summary>
    public static int? DwellSoFar(VisitRow row, DateTime nowUtc) =>
        row.Status == VisitStatus.InYard
            ? Domain.DwellCalculator.Days(row.GateInAtUtc, nowUtc < row.GateInAtUtc ? row.GateInAtUtc : nowUtc, DepotTimeZone.Dhaka)
            : null;
}
