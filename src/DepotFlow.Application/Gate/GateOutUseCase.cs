using DepotFlow.Application.Billing;
using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Gate;

public sealed class GateOutUseCase(
    IDepotFlowDbContext db,
    IValidator<GateOutRequest> validator,
    ChargeCalculator chargeCalculator,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<VisitDto>> ExecuteAsync(long visitId, GateOutRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        // 1. Load the visit.
        var visit = await db.Visits
            .Include(v => v.Container)
            .Include(v => v.ShippingLine)
            .FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken);

        if (visit is null)
        {
            return GateErrors.VisitNotFound;
        }

        if (visit.Status == VisitStatus.Released)
        {
            return GateErrors.VisitAlreadyReleased;
        }

        // 2. Work out the charge before changing anything. No active tariff means nothing is touched:
        //    the visit stays in the yard and keeps its slot.
        var now = clock.UtcNow;
        var quote = await chargeCalculator.QuoteAsync(visit, now, cancellationToken);
        if (!quote.IsSuccess)
        {
            return quote.Error;
        }

        // 3. Release the visit (this also frees its slot) and issue the invoice. Both changes are saved by the
        //    single SaveChangesAsync below, which SQL Server runs as one transaction: either the visit is released
        //    AND the invoice exists, or neither happened.
        visit.GateOut(now, request.TruckNumber!, request.DamageNotes, currentUser.UserId);
        var invoice = Invoice.Issue(visit, quote.Value.Tariff, quote.Value.DwellDays, quote.Value.Charge, now);
        db.Invoices.Add(invoice);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IndexNames.InvoiceVisit)
        {
            // A simultaneous gate-out already issued this visit's invoice; our whole save was rolled back.
            return GateErrors.VisitAlreadyReleased;
        }

        return visit.ToDto(quote.Value.DwellDays, invoice);
    }
}
