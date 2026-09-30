using DepotFlow.Application.Common;
using DepotFlow.Application.ShippingLines;
using DepotFlow.Domain;
using DepotFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Gate;

public sealed class GateInUseCase(
    IDepotFlowDbContext db,
    IValidator<GateInRequest> validator,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<VisitDto>> ExecuteAsync(GateInRequest request, CancellationToken cancellationToken)
    {
        // 1. Request shape.
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        // 2. Container number (ISO 6346).
        if (!ContainerNumber.TryCreate(request.ContainerNumber, out var number, out var reason))
        {
            return GateErrors.InvalidContainerNumber(reason);
        }

        // 3. Shipping line must exist and be active.
        var line = await db.ShippingLines.FirstOrDefaultAsync(x => x.Id == request.ShippingLineId, cancellationToken);
        if (line is null)
        {
            return ShippingLineErrors.NotFound;
        }

        if (!line.IsActive)
        {
            return GateErrors.ShippingLineInactive;
        }

        // 4. Find or create the container; a known container must keep its size.
        var now = clock.UtcNow;
        var container = await db.Containers.FirstOrDefaultAsync(x => x.Number == number.Value, cancellationToken);
        if (container is null)
        {
            container = new Container(number, request.SizeFeet!.Value, now);
            db.Containers.Add(container);
        }
        else
        {
            if (container.SizeFeet != request.SizeFeet)
            {
                return GateErrors.ContainerSizeMismatch;
            }

            // 5. Friendly check for an active visit; the filtered unique index is the real guard.
            var alreadyInYard = await db.Visits.AnyAsync(
                v => v.ContainerId == container.Id && v.Status == VisitStatus.InYard, cancellationToken);
            if (alreadyInYard)
            {
                return GateErrors.ContainerAlreadyInYard;
            }
        }

        // 6. Create the visit.
        var visit = new Visit(
            container, line, now, request.TruckNumber!, request.SealNumber!, request.DamageNotes, currentUser.UserId);
        db.Visits.Add(visit);

        // 7. Save. A concurrent request that won the race trips a unique index; report it as a normal 409.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return GateErrors.ContainerAlreadyInYard;
        }

        return visit.ToDto();
    }
}
