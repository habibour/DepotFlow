using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.Invoices;

public sealed record InvoiceLineDto(string Description, int FromDay, int ToDay, int Days, decimal RatePerDay, decimal Amount);

public sealed record InvoiceListItemDto(
    long Id,
    string InvoiceNumber,
    long VisitId,
    string ContainerNumber,
    string ShippingLineCode,
    DateTime IssuedAtUtc,
    decimal Total,
    string Currency,
    InvoiceStatus Status);

public sealed record InvoiceDetailDto(
    long Id,
    string InvoiceNumber,
    long VisitId,
    string ContainerNumber,
    int SizeFeet,
    string ShippingLineCode,
    DateTime GateInAtUtc,
    DateTime? GateOutAtUtc,
    DateTime IssuedAtUtc,
    int DwellDays,
    int FreeDays,
    string StrategyKey,
    string Currency,
    decimal Total,
    InvoiceStatus Status,
    DateTime? PaidAtUtc,
    string? PaidByUserId,
    IReadOnlyList<InvoiceLineDto> Lines);

internal static class InvoiceMapping
{
    // Expression form so EF can translate it; the compiled copy maps an invoice that is already loaded
    // (with Visit.Container, Visit.ShippingLine and Lines included).
    public static readonly System.Linq.Expressions.Expression<Func<Invoice, InvoiceDetailDto>> DetailProjection = i => new InvoiceDetailDto(
        i.Id, i.InvoiceNumber, i.VisitId, i.Visit.Container.Number, i.Visit.Container.SizeFeet, i.Visit.ShippingLine.Code,
        i.Visit.GateInAtUtc, i.Visit.GateOutAtUtc, i.IssuedAtUtc, i.DwellDays, i.FreeDays, i.StrategyKey, i.Currency,
        i.Total, i.Status, i.PaidAtUtc, i.PaidByUserId,
        i.Lines.OrderBy(l => l.FromDay)
            .Select(l => new InvoiceLineDto(l.Description, l.FromDay, l.ToDay, l.Days, l.RatePerDay, l.Amount))
            .ToList());

    private static readonly Func<Invoice, InvoiceDetailDto> Compiled = DetailProjection.Compile();

    public static InvoiceDetailDto ToDetail(this Invoice invoice) => Compiled(invoice);
}
