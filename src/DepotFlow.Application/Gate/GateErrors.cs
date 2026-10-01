using DepotFlow.Application.Common;

namespace DepotFlow.Application.Gate;

public static class GateErrors
{
    public static Error InvalidContainerNumber(string reason) =>
        Error.Unprocessable("invalid_container_number", reason);

    public static readonly Error ShippingLineInactive =
        Error.Unprocessable("shipping_line_inactive", "This shipping line is inactive.");

    public static readonly Error ContainerSizeMismatch =
        Error.Conflict("container_size_mismatch", "This container was previously recorded with a different size.");

    public static readonly Error ContainerAlreadyInYard =
        Error.Conflict("container_already_in_yard", "This container already has an active visit.");

    public static readonly Error VisitNotFound =
        Error.NotFound("visit_not_found", "Visit was not found.");

    public static readonly Error VisitAlreadyReleased =
        Error.Conflict("visit_already_released", "This visit has already been released.");

    public static readonly Error YardFull =
        Error.Conflict("yard_full", "There is no free slot where this container can be placed.");

    public static readonly Error SlotConflict =
        Error.Conflict("slot_conflict", "The slot was taken by another request at the same time. Please try again.");

    public static readonly Error SlotOccupied =
        Error.Conflict("slot_occupied", "The target slot is already occupied.");

    public static readonly Error VisitNotInYard =
        Error.Conflict("visit_not_in_yard", "This visit is not in the yard.");

    public static readonly Error SlotNotFound =
        Error.NotFound("slot_not_found", "Yard slot was not found.");

    public static readonly Error StackingRuleViolated =
        Error.Unprocessable("stacking_rule_violated", "A container can only go on tier 2 or higher when the slot below it is occupied.");

    public static readonly Error SameSlot =
        Error.Unprocessable("same_slot", "The container is already in that slot.");
}
