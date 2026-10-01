using DepotFlow.Application.Common;

namespace DepotFlow.Application.Invoices;

public static class InvoiceErrors
{
    public static readonly Error NotFound = Error.NotFound("invoice_not_found", "Invoice was not found.");

    public static readonly Error AlreadyPaid = Error.Conflict("invoice_already_paid", "This invoice has already been paid.");
}
