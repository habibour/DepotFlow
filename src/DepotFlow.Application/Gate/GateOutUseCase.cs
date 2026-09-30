using DepotFlow.Application.Common;
using DepotFlow.Domain;
using DepotFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Gate;

public sealed class GateOutUseCase(
    IDepotFlowDbContext db,
    IValidator<GateOutRequest> validator,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<VisitDto>> ExecuteAsync(long visitId, GateOutRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        // Step 1: load the visit.
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

        // Step 2: release the visit. (Day 2 adds freeing the slot and creating the invoice here,
        // inside the same save, so they commit or fail together.)
        var now = clock.UtcNow;
        visit.GateOut(now, request.TruckNumber!, request.DamageNotes, currentUser.UserId);

        // Step 3: save and report dwell time.
        await db.SaveChangesAsync(cancellationToken);

        var dwellDays = DwellCalculator.Days(visit.GateInAtUtc, now, DepotTimeZone.Dhaka);
        return visit.ToDto(dwellDays);
    }
}
