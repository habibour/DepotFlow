using DepotFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Invoices;

public sealed class GetInvoiceUseCase(IDepotFlowDbContext db)
{
    public async Task<Result<InvoiceDetailDto>> ExecuteAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await db.Invoices.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(InvoiceMapping.DetailProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null ? InvoiceErrors.NotFound : dto;
    }
}
