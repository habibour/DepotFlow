using DepotFlow.Domain.Billing;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Domain.Tests;

// Covers spec S2-09 and S2-10 (invoice contents; zero-total invoices are born Paid).
public class InvoiceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 6, 0, 0, DateTimeKind.Utc);

    private static readonly TariffSnapshot Standard = new(
        StrategyKeys.Tiered, 4, "BDT",
        [new TariffTierSnapshot(5, 10, 200m), new TariffTierSnapshot(11, null, 400m)]);

    private static Visit NewVisit()
    {
        ContainerNumber.TryCreate("CSQU3054383", out var number, out _);
        return new Visit(new Container(number!, 20, Now), new ShippingLine("CMDU", "CMA CGM", Now), Now, "T1", "S1", null, "u1");
    }

    private static Invoice IssueFor(int dwellDays)
    {
        var charge = new TieredStrategy().Calculate(Standard, dwellDays);
        return Invoice.Issue(NewVisit(), Standard, dwellDays, charge, Now);
    }

    [Fact]
    public void A_12_day_stay_is_issued_with_two_lines_and_the_tariff_copied_in()
    {
        var invoice = IssueFor(12);

        Assert.Equal(2000m, invoice.Total);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Null(invoice.PaidAtUtc);
        Assert.Equal((12, 4, "Tiered", "BDT"), (invoice.DwellDays, invoice.FreeDays, invoice.StrategyKey, invoice.Currency));
        Assert.Equal(["Days 5-10 at 200.00", "Days 11-12 at 400.00"], invoice.Lines.Select(l => l.Description));
        Assert.Equal([1200m, 800m], invoice.Lines.Select(l => l.Amount));
    }

    [Fact]
    public void A_zero_total_invoice_is_born_paid_at_its_issue_time()
    {
        var invoice = IssueFor(3);

        Assert.Equal(0m, invoice.Total);
        Assert.Empty(invoice.Lines);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(Now, invoice.PaidAtUtc);
    }

    [Fact]
    public void Paying_records_who_and_when_and_cannot_be_done_twice()
    {
        var invoice = IssueFor(12);

        invoice.Pay(Now.AddDays(1), "billing-1");

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(Now.AddDays(1), invoice.PaidAtUtc);
        Assert.Equal("billing-1", invoice.PaidByUserId);
        Assert.Throws<DomainException>(() => invoice.Pay(Now.AddDays(2), "billing-2"));
    }
}
