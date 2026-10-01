using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Invoices;

public sealed class PayInvoiceUseCase(IDepotFlowDbContext db, IClock clock, ICurrentUser currentUser)
{
    public async Task<Result<InvoiceDetailDto>> ExecuteAsync(long id, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Visit).ThenInclude(v => v.Container)
            .Include(i => i.Visit).ThenInclude(v => v.ShippingLine)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice is null)
        {
            return InvoiceErrors.NotFound;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return InvoiceErrors.AlreadyPaid;
        }

        invoice.Pay(clock.UtcNow, currentUser.UserId);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else paid it between our read and our save (Status is a concurrency token).
            return InvoiceErrors.AlreadyPaid;
        }

        return invoice.ToDetail();
    }
}
