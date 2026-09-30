using DepotFlow.Application.Common;

namespace DepotFlow.Application.ShippingLines;

public static class ShippingLineErrors
{
    public static readonly Error NotFound =
        Error.NotFound("shipping_line_not_found", "Shipping line was not found.");

    public static readonly Error CodeExists =
        Error.Conflict("shipping_line_code_exists", "A shipping line with this code already exists.");
}
