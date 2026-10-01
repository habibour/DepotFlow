using System.Globalization;
using DepotFlow.Domain.Billing;

namespace DepotFlow.Domain.Entities;

public class InvoiceLine
{
    private InvoiceLine() { }   // for EF Core

    public InvoiceLine(ChargeLine charge)
    {
        Description = string.Create(
            CultureInfo.InvariantCulture, $"Days {charge.FromDay}-{charge.ToDay} at {charge.RatePerDay:0.00}");
        FromDay = charge.FromDay;
        ToDay = charge.ToDay;
        Days = charge.Days;
        RatePerDay = charge.RatePerDay;
        Amount = charge.Amount;
    }

    public long Id { get; private set; }
    public long InvoiceId { get; private set; }
    public string Description { get; private set; } = null!;
    public int FromDay { get; private set; }
    public int ToDay { get; private set; }
    public int Days { get; private set; }
    public decimal RatePerDay { get; private set; }
    public decimal Amount { get; private set; }
}
