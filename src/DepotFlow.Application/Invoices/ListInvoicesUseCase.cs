using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Invoices;

public sealed class ListInvoicesUseCase(IDepotFlowDbContext db)
{
    public async Task<PagedResult<InvoiceListItemDto>> ExecuteAsync(
        InvoiceStatus? status, int? shippingLineId, DateTime? issuedFrom, DateTime? issuedTo, string? invoiceNumber,
        int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.Invoices.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (shippingLineId is not null)
        {
            query = query.Where(i => i.ShippingLineId == shippingLineId);
        }

        if (issuedFrom is not null)
        {
            query = query.Where(i => i.IssuedAtUtc >= issuedFrom);
        }

        if (issuedTo is not null)
        {
            query = query.Where(i => i.IssuedAtUtc <= issuedTo);
        }

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var number = invoiceNumber.Trim().ToUpperInvariant();   // exact match, not a prefix
            query = query.Where(i => i.InvoiceNumber == number);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.IssuedAtUtc).ThenByDescending(i => i.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(i => new InvoiceListItemDto(
                i.Id, i.InvoiceNumber, i.VisitId, i.Visit.Container.Number, i.Visit.ShippingLine.Code,
                i.IssuedAtUtc, i.Total, i.Currency, i.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceListItemDto>(items, p, size, total);
    }
}
