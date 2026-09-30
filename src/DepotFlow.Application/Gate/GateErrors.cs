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
}
