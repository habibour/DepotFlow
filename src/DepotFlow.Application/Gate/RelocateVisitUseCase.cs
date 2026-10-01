using DepotFlow.Application.Common;
using DepotFlow.Domain.Entities;
using DepotFlow.Domain.Yard;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Gate;

public sealed class RelocateVisitUseCase(IDepotFlowDbContext db, IValidator<RelocateRequest> validator)
{
    public async Task<Result<VisitDto>> ExecuteAsync(long visitId, RelocateRequest request, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        // 1. The visit must exist and still be in the yard.
        var visit = await db.Visits
            .Include(v => v.Container)
            .Include(v => v.ShippingLine)
            .Include(v => v.YardSlot)
            .FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken);
        if (visit is null)
        {
            return GateErrors.VisitNotFound;
        }

        if (visit.Status != VisitStatus.InYard)
        {
            return GateErrors.VisitNotInYard;
        }

        // 2. The target slot must exist and be a different slot. (Same-slot is checked before "occupied":
        //    the container's own slot counts as occupied, so otherwise same_slot could never be reported.)
        var target = await db.YardSlots.FirstOrDefaultAsync(s => s.Id == request.TargetSlotId, cancellationToken);
        if (target is null)
        {
            return GateErrors.SlotNotFound;
        }

        if (visit.YardSlotId == target.Id)
        {
            return GateErrors.SameSlot;
        }

        // 3. Occupied, or stacking rule broken? Only the target and the slot below it matter. This visit's own
        //    current slot is left out: it is about to be vacated, so it cannot support the target.
        var occupiedTiers = await db.Visits.AsNoTracking()
            .Where(v => v.Status == VisitStatus.InYard && v.Id != visit.Id && v.YardSlot != null
                        && v.YardSlot.Block == target.Block && v.YardSlot.Row == target.Row && v.YardSlot.Bay == target.Bay
                        && (v.YardSlot.Tier == target.Tier || v.YardSlot.Tier == target.Tier - 1))
            .Select(v => v.YardSlot!.Tier)
            .ToListAsync(cancellationToken);
        var occupied = occupiedTiers.Select(t => new SlotPosition(target.Block, target.Row, target.Bay, t)).ToHashSet();

        switch (StackRules.Evaluate(target.Position, occupied))
        {
            case Placement.Occupied:
                return GateErrors.SlotOccupied;
            case Placement.StackingRuleViolated:
                return GateErrors.StackingRuleViolated;
        }

        // 4. Move. If another request grabbed the slot between our check and our save, the unique index says so.
        visit.AssignSlot(target);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IndexNames.VisitSlotActive)
        {
            return GateErrors.SlotOccupied;
        }

        return visit.ToDto();
    }
}
