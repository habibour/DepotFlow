using DepotFlow.Application.Common;
using DepotFlow.Application.ShippingLines;
using DepotFlow.Application.Tariffs;
using DepotFlow.Domain;
using DepotFlow.Domain.Entities;
using DepotFlow.Domain.Yard;
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

        // Steps 3 to 8 run at most twice: if another request takes the slot we picked in the meantime,
        // the unique index rejects our save, and we start again from fresh data (spec S2-02).
        for (var attempt = 1; ; attempt++)
        {
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

            // 6. There must be an active tariff, so the gate-out invoice can always be calculated.
            var hasTariff = await db.Tariffs.AnyAsync(
                t => t.ShippingLineId == line.Id && t.SizeFeet == request.SizeFeet && t.IsActive, cancellationToken);
            if (!hasTariff)
            {
                return TariffErrors.NoActiveTariff;
            }

            // 7. Pick the first free slot that obeys the stacking rule.
            var slot = await FindFreeSlotAsync(cancellationToken);
            if (slot is null)
            {
                return GateErrors.YardFull;
            }

            var visit = new Visit(
                container, line, now, request.TruckNumber!, request.SealNumber!, request.DamageNotes, currentUser.UserId);
            visit.AssignSlot(slot);
            db.Visits.Add(visit);

            // 8. Save. Losing a race trips a unique index; which one tells us what to do.
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return visit.ToDto();
            }
            catch (UniqueConstraintViolationException ex)
            {
                db.ResetChanges();   // forget the failed visit (and new container) before trying again or returning

                if (ex.ConstraintName != IndexNames.VisitSlotActive)
                {
                    return GateErrors.ContainerAlreadyInYard;   // container index or active-visit index: same container won
                }

                if (attempt == 2)
                {
                    return GateErrors.SlotConflict;
                }
            }
        }
    }

    /// <summary>
    /// Walks the free slots in block, row, bay, tier order and returns the first one the stacking rule allows.
    /// Streaming stops at the first match, so this is cheap while the yard has room.
    /// </summary>
    private async Task<YardSlot?> FindFreeSlotAsync(CancellationToken cancellationToken)
    {
        var occupiedRows = await db.Visits.AsNoTracking()
            .Where(v => v.Status == VisitStatus.InYard && v.YardSlot != null)
            .Select(v => new { v.YardSlot!.Block, v.YardSlot.Row, v.YardSlot.Bay, v.YardSlot.Tier })
            .ToListAsync(cancellationToken);
        var occupied = occupiedRows.Select(o => new SlotPosition(o.Block, o.Row, o.Bay, o.Tier)).ToHashSet();

        var freeSlots = db.YardSlots
            .Where(s => !db.Visits.Any(v => v.YardSlotId == s.Id && v.Status == VisitStatus.InYard))
            .OrderBy(s => s.Block).ThenBy(s => s.Row).ThenBy(s => s.Bay).ThenBy(s => s.Tier)
            .AsAsyncEnumerable();

        await foreach (var slot in freeSlots.WithCancellation(cancellationToken))
        {
            if (StackRules.CanPlace(slot.Position, occupied))
            {
                return slot;
            }
        }

        return null;
    }
}
