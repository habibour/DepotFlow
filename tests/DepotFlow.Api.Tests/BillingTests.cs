using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Billing;
using DepotFlow.Application.Common;
using DepotFlow.Application.Containers;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Invoices;
using DepotFlow.Application.Security;
using DepotFlow.Application.Yard;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Api.Tests;

// Covers spec S2-08 to S2-11 (invoices at gate-out, charge preview, paying) and S2-T-YARD-7.
// Standard tariff for lines 1 and 2 (set up by ApiTestBase): free 4 days; days 5-10 at 200; day 11 on at 400.
// Tests create their clients first, then move the fake clock: login tokens are stamped with the fake clock.
[Collection(ApiCollection.Name)]
public partial class BillingTests(ApiFactory factory) : ApiTestBase(factory)
{
    [GeneratedRegex(@"^INV-\d{4}-\d{7}$")]
    private static partial Regex InvoiceNumberPattern();

    private static async Task<VisitDto> GateInAsync(HttpClient client, int sizeFeet = 20)
    {
        var response = await client.PostAsJsonAsync("/api/v1/visits/gate-in", TestData.GateInBody(TestData.NewContainerNumber(), sizeFeet));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestData.ReadAsync<VisitDto>(response);
    }

    private static Task<HttpResponseMessage> GateOutAsync(HttpClient client, long visitId) =>
        client.PostAsJsonAsync($"/api/v1/visits/{visitId}/gate-out", TestData.GateOutBody());

    private static async Task<VisitDto> GateOutOkAsync(HttpClient client, long visitId)
    {
        var response = await GateOutAsync(client, visitId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TestData.ReadAsync<VisitDto>(response);
    }

    private static async Task<InvoiceDetailDto> GetInvoiceAsync(HttpClient client, long id) =>
        await TestData.ReadAsync<InvoiceDetailDto>(await client.GetAsync($"/api/v1/invoices/{id}"));

    [Fact]
    public async Task S2_T_BILL_2_full_journey_with_a_tiered_tariff_over_12_days_gives_2000_in_two_lines()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);

        Factory.Clock.AdvanceToDhakaMidday(0);
        var visit = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(11);   // day 1 is the gate-in day, so this is day 12
        var released = await GateOutOkAsync(clerk, visit.Id);

        Assert.Equal(12, released.DwellDays);
        Assert.Null(released.YardSlot);   // the slot is free again
        var summary = released.Invoice!;
        Assert.Equal((2000m, "BDT", InvoiceStatus.Issued), (summary.Total, summary.Currency, summary.Status));
        Assert.Matches(InvoiceNumberPattern(), summary.InvoiceNumber);

        var invoice = await GetInvoiceAsync(billing, summary.Id);
        Assert.Equal(visit.ContainerNumber, invoice.ContainerNumber);
        Assert.Equal((12, 4, "Tiered"), (invoice.DwellDays, invoice.FreeDays, invoice.StrategyKey));
        Assert.Equal(
            [new InvoiceLineDto("Days 5-10 at 200.00", 5, 10, 6, 200m, 1200m), new InvoiceLineDto("Days 11-12 at 400.00", 11, 12, 2, 400m, 800m)],
            invoice.Lines);
    }

    [Fact]
    public async Task S2_T_BILL_3_pay_the_invoice_then_paying_again_is_409_and_a_gate_clerk_gets_403()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var visit = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(11);
        var invoiceId = (await GateOutOkAsync(clerk, visit.Id)).Invoice!.Id;

        var forbidden = await clerk.PostAsync($"/api/v1/invoices/{invoiceId}/pay", null);
        var paid = await billing.PostAsync($"/api/v1/invoices/{invoiceId}/pay", null);
        var again = await billing.PostAsync($"/api/v1/invoices/{invoiceId}/pay", null);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var invoice = await TestData.ReadAsync<InvoiceDetailDto>(paid);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAtUtc);
        Assert.NotNull(invoice.PaidByUserId);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("invoice_already_paid", await TestData.ProblemCodeAsync(again));
    }

    [Fact]
    public async Task S2_T_BILL_3_two_simultaneous_payments_give_one_200_and_one_409()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var visit = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(11);
        var invoiceId = (await GateOutOkAsync(clerk, visit.Id)).Invoice!.Id;

        var responses = await Task.WhenAll(
            billing.PostAsync($"/api/v1/invoices/{invoiceId}/pay", null),
            billing.PostAsync($"/api/v1/invoices/{invoiceId}/pay", null));

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Conflict],
            responses.Select(r => r.StatusCode).OrderBy(s => s));
    }

    [Fact]
    public async Task S2_T_BILL_4_a_stay_within_the_free_days_costs_nothing_and_is_born_paid()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var visit = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(2);   // day 3 of 4 free days

        var released = await GateOutOkAsync(clerk, visit.Id);

        Assert.Equal(3, released.DwellDays);
        Assert.Equal((0m, InvoiceStatus.Paid), (released.Invoice!.Total, released.Invoice.Status));
    }

    [Fact]
    public async Task S2_T_BILL_5_changing_the_tariff_leaves_old_invoices_alone_and_new_gate_outs_use_the_new_one()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var first = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(11);
        var oldInvoiceId = (await GateOutOkAsync(clerk, first.Id)).Invoice!.Id;

        // New tariff for line 1 / 20 ft: flat, no free days, 100 per day. It replaces the active one.
        var created = await billing.PostAsJsonAsync("/api/v1/shipping-lines/1/tariffs", new
        {
            sizeFeet = 20, strategyKey = "Flat", freeDays = 0,
            tiers = new[] { new { fromDay = 1, toDay = (int?)null, ratePerDay = 100m } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var second = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(11);
        var newInvoice = (await GateOutOkAsync(clerk, second.Id)).Invoice!;

        Assert.Equal(1200m, newInvoice.Total);   // 12 days x 100
        var old = await GetInvoiceAsync(billing, oldInvoiceId);
        Assert.Equal((2000m, "Tiered", 2), (old.Total, old.StrategyKey, old.Lines.Count));
        Assert.Equal("Flat", (await GetInvoiceAsync(billing, newInvoice.Id)).StrategyKey);
    }

    [Fact]
    public async Task S2_T_BILL_6_gate_out_without_an_active_tariff_is_422_and_changes_nothing()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        var visit = await GateInAsync(clerk);

        var tariffs = await TestData.ReadAsync<List<DepotFlow.Application.Tariffs.TariffDto>>(
            await billing.GetAsync("/api/v1/shipping-lines/1/tariffs"));
        var active20 = tariffs.Single(t => t.IsActive && t.SizeFeet == 20);
        Assert.Equal(HttpStatusCode.OK, (await billing.PutAsJsonAsync($"/api/v1/tariffs/{active20.Id}", new { isActive = false })).StatusCode);

        var response = await GateOutAsync(clerk, visit.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("no_active_tariff", await TestData.ProblemCodeAsync(response));
        var container = await TestData.ReadAsync<ContainerDetailDto>(await clerk.GetAsync($"/api/v1/containers/{visit.ContainerId}"));
        Assert.Equal(VisitStatus.InYard, container.Visits.Single().Status);
        var slots = (await TestData.ReadAsync<PagedResult<YardSlotDto>>(await planner.GetAsync("/api/v1/yard/slots?occupied=true"))).Items;
        Assert.Equal(visit.Id, Assert.Single(slots).Occupant!.VisitId);
        Assert.Equal(0, (await TestData.ReadAsync<PagedResult<InvoiceListItemDto>>(await billing.GetAsync("/api/v1/invoices"))).TotalCount);
    }

    // The stronger proof of atomicity: the invoice INSERT fails (a duplicate for the visit already exists), and
    // the visit UPDATE that came before it in the same save must be undone too.
    [Fact]
    public async Task S2_T_BILL_6_when_the_invoice_cannot_be_saved_the_visit_stays_in_the_yard_with_its_slot()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        var visit = await GateInAsync(clerk);
        await Factory.InsertInvoiceForVisitAsync(visit.Id);

        var response = await GateOutAsync(clerk, visit.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var container = await TestData.ReadAsync<ContainerDetailDto>(await clerk.GetAsync($"/api/v1/containers/{visit.ContainerId}"));
        Assert.Equal(VisitStatus.InYard, container.Visits.Single().Status);
        Assert.Null(container.Visits.Single().GateOutAtUtc);
        var slots = (await TestData.ReadAsync<PagedResult<YardSlotDto>>(await planner.GetAsync("/api/v1/yard/slots?occupied=true"))).Items;
        Assert.Equal(visit.Id, Assert.Single(slots).Occupant!.VisitId);
    }

    [Fact]
    public async Task S2_T_BILL_6_two_simultaneous_gate_outs_give_one_200_one_409_and_one_invoice()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var visit = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(11);

        var responses = await Task.WhenAll(GateOutAsync(clerk, visit.Id), GateOutAsync(clerk, visit.Id));

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Conflict],
            responses.Select(r => r.StatusCode).OrderBy(s => s));
        Assert.Equal(1, (await TestData.ReadAsync<PagedResult<InvoiceListItemDto>>(await billing.GetAsync("/api/v1/invoices"))).TotalCount);
    }

    [Fact]
    public async Task S2_T_BILL_7_charge_preview_matches_the_invoice_gate_out_then_produces()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var visit = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(6);   // 7 days so far

        var forbidden = await clerk.GetAsync($"/api/v1/visits/{visit.Id}/charge-preview");
        var previewResponse = await billing.GetAsync($"/api/v1/visits/{visit.Id}/charge-preview");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await TestData.ReadAsync<ChargePreviewDto>(previewResponse);
        var released = await GateOutOkAsync(clerk, visit.Id);
        var invoice = await GetInvoiceAsync(billing, released.Invoice!.Id);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(7, preview.DwellDays);
        Assert.Equal(600m, preview.Total);   // days 5, 6, 7 at 200
        Assert.Equal((preview.Total, preview.DwellDays), (invoice.Total, invoice.DwellDays));
        Assert.Equal(preview.Lines.Select(l => (l.FromDay, l.ToDay, l.Days, l.RatePerDay, l.Amount)),
                     invoice.Lines.Select(l => (l.FromDay, l.ToDay, l.Days, l.RatePerDay, l.Amount)));

        var afterRelease = await billing.GetAsync($"/api/v1/visits/{visit.Id}/charge-preview");
        Assert.Equal(HttpStatusCode.Conflict, afterRelease.StatusCode);
        Assert.Equal("visit_not_in_yard", await TestData.ProblemCodeAsync(afterRelease));
    }

    [Fact]
    public async Task S2_T_YARD_7_gate_out_frees_the_slot_and_the_next_gate_in_reuses_it()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var first = await GateInAsync(clerk);
        Assert.Equal("A-01-01-1", first.YardSlot!.Code);
        await GateOutOkAsync(clerk, first.Id);

        var next = await GateInAsync(clerk);

        Assert.Equal("A-01-01-1", next.YardSlot!.Code);
    }

    [Fact]
    public async Task Invoice_list_filters_and_roles()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var costly = await GateInAsync(clerk);
        var free = await GateInAsync(clerk);
        Factory.Clock.AdvanceToDhakaMidday(1);
        var freeInvoice = (await GateOutOkAsync(clerk, free.Id)).Invoice!;   // 2 days: free, Paid
        Factory.Clock.AdvanceToDhakaMidday(10);
        var costlyInvoice = (await GateOutOkAsync(clerk, costly.Id)).Invoice!;   // 12 days: 2,000, Issued

        async Task<PagedResult<InvoiceListItemDto>> List(string query) =>
            await TestData.ReadAsync<PagedResult<InvoiceListItemDto>>(await billing.GetAsync("/api/v1/invoices" + query));

        Assert.Equal(2, (await List("")).TotalCount);
        Assert.Equal([costlyInvoice.Id], (await List("?status=Issued")).Items.Select(i => i.Id));
        Assert.Equal([freeInvoice.Id], (await List("?status=Paid")).Items.Select(i => i.Id));
        Assert.Equal([costlyInvoice.Id], (await List($"?invoiceNumber={costlyInvoice.InvoiceNumber}")).Items.Select(i => i.Id));
        Assert.Equal(2, (await List("?shippingLineId=1")).TotalCount);
        Assert.Equal(0, (await List("?shippingLineId=2")).TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/v1/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await billing.GetAsync("/api/v1/invoices/999999")).StatusCode);
    }
}
