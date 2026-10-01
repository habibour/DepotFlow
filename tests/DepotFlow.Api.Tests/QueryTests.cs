using System.Net;
using System.Net.Http.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Common;
using DepotFlow.Application.Containers;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Security;
using DepotFlow.Application.Visits;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Api.Tests;

// Covers spec S2-16 (search and filters). Shipping lines 1 and 2 have tariffs; the test yard has 4 slots.
[Collection(ApiCollection.Name)]
public class QueryTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static async Task<VisitDto> GateInAsync(HttpClient client, int lineId, int sizeFeet = 20, string? number = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/visits/gate-in", new
        {
            containerNumber = number ?? TestData.NewContainerNumber(),
            sizeFeet,
            shippingLineId = lineId,
            truckNumber = "DHK-TA-11-2345",
            sealNumber = "SL889201"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestData.ReadAsync<VisitDto>(response);
    }

    private static async Task<PagedResult<VisitListItemDto>> ListVisitsAsync(HttpClient client, string query = "") =>
        await TestData.ReadAsync<PagedResult<VisitListItemDto>>(await client.GetAsync("/api/v1/visits" + query));

    [Fact]
    public async Task S2_T_FILTER_1_visits_filtered_by_status_and_shipping_line_with_correct_paging_totals()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var line1Active = await GateInAsync(clerk, lineId: 1);
        var line1Other = await GateInAsync(clerk, lineId: 1);
        var line2 = await GateInAsync(clerk, lineId: 2);
        await clerk.PostAsJsonAsync($"/api/v1/visits/{line1Other.Id}/gate-out", TestData.GateOutBody());

        var match = await ListVisitsAsync(clerk, "?status=InYard&shippingLineId=1");
        var line1All = await ListVisitsAsync(clerk, "?shippingLineId=1&pageSize=1&page=2");
        var released = await ListVisitsAsync(clerk, "?status=Released");

        Assert.Equal([line1Active.Id], match.Items.Select(v => v.Id));
        Assert.Equal(1, match.TotalCount);
        Assert.Equal((2, 2, 1), (line1All.TotalCount, line1All.Page, line1All.Items.Count));   // 2 visits, page 2 of size 1
        Assert.Equal([line1Other.Id], released.Items.Select(v => v.Id));
        Assert.DoesNotContain(line2.Id, match.Items.Select(v => v.Id));
    }

    [Fact]
    public async Task S2_T_FILTER_2_containers_with_inYard_true_are_only_those_with_an_active_visit()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var inYard = await GateInAsync(clerk, lineId: 1);
        var left = await GateInAsync(clerk, lineId: 1);
        await clerk.PostAsJsonAsync($"/api/v1/visits/{left.Id}/gate-out", TestData.GateOutBody());

        async Task<List<int>> Ids(string query) => (await TestData.ReadAsync<PagedResult<ContainerListItemDto>>(
            await clerk.GetAsync("/api/v1/containers" + query))).Items.Select(c => c.Id).ToList();

        Assert.Equal([inYard.ContainerId], await Ids("?inYard=true"));
        Assert.Equal([left.ContainerId], await Ids("?inYard=false"));
        Assert.Equal(2, (await Ids("")).Count);
    }

    [Fact]
    public async Task Containers_can_be_filtered_by_size()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var small = await GateInAsync(clerk, lineId: 1, sizeFeet: 20);
        var big = await GateInAsync(clerk, lineId: 1, sizeFeet: 40);

        var forty = await TestData.ReadAsync<PagedResult<ContainerListItemDto>>(await clerk.GetAsync("/api/v1/containers?sizeFeet=40"));

        Assert.Equal([big.ContainerId], forty.Items.Select(c => c.Id));
        Assert.DoesNotContain(small.ContainerId, forty.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task Visits_can_be_sorted_and_filtered_by_container_prefix_and_gate_in_time()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var first = await GateInAsync(clerk, lineId: 1);
        await Task.Delay(20);   // distinct gate-in timestamps
        var second = await GateInAsync(clerk, lineId: 1);

        var oldestFirst = await ListVisitsAsync(clerk);
        var newestFirst = await ListVisitsAsync(clerk, "?sort=-gateInAtUtc");
        var byPrefix = await ListVisitsAsync(clerk, $"?containerNumber={second.ContainerNumber[..8].ToLowerInvariant()}");
        var afterFirst = await ListVisitsAsync(clerk, $"?gateInFrom={Uri.EscapeDataString(second.GateInAtUtc.ToString("O"))}");
        var beforeSecond = await ListVisitsAsync(clerk, $"?gateInTo={Uri.EscapeDataString(first.GateInAtUtc.ToString("O"))}");

        Assert.Equal([first.Id, second.Id], oldestFirst.Items.Select(v => v.Id));
        Assert.Equal([second.Id, first.Id], newestFirst.Items.Select(v => v.Id));
        Assert.Equal(2, byPrefix.TotalCount);   // both test containers share the "TSTU00.." prefix
        Assert.Equal([second.Id], afterFirst.Items.Select(v => v.Id));
        Assert.Equal([first.Id], beforeSecond.Items.Select(v => v.Id));
    }

    [Fact]
    public async Task Visit_items_show_slot_code_and_dwell_days_so_far_only_for_visits_in_the_yard()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        Factory.Clock.AdvanceToDhakaMidday(0);
        var staying = await GateInAsync(clerk, lineId: 1);
        var leaving = await GateInAsync(clerk, lineId: 1);
        await clerk.PostAsJsonAsync($"/api/v1/visits/{leaving.Id}/gate-out", TestData.GateOutBody());
        Factory.Clock.AdvanceToDhakaMidday(2);   // the staying container is now on day 3

        var items = (await ListVisitsAsync(clerk)).Items.ToDictionary(v => v.Id);

        Assert.Equal((VisitStatus.InYard, 3, "A-01-01-1"), (items[staying.Id].Status, items[staying.Id].DwellDays, items[staying.Id].SlotCode));
        Assert.Null(items[leaving.Id].DwellDays);
        Assert.Null(items[leaving.Id].SlotCode);
        Assert.NotNull(items[leaving.Id].GateOutAtUtc);
    }

    [Fact]
    public async Task Visit_detail_includes_the_invoice_summary_once_the_visit_is_released()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var visit = await GateInAsync(clerk, lineId: 1);
        var before = await TestData.ReadAsync<VisitDetailDto>(await clerk.GetAsync($"/api/v1/visits/{visit.Id}"));
        var released = await TestData.ReadAsync<VisitDto>(await clerk.PostAsJsonAsync($"/api/v1/visits/{visit.Id}/gate-out", TestData.GateOutBody()));

        var after = await TestData.ReadAsync<VisitDetailDto>(await clerk.GetAsync($"/api/v1/visits/{visit.Id}"));
        var missing = await clerk.GetAsync("/api/v1/visits/999999");

        Assert.Null(before.Invoice);
        Assert.Equal(1, before.DwellDays);
        Assert.Equal(released.Invoice!.InvoiceNumber, after.Invoice!.InvoiceNumber);
        Assert.Equal(VisitStatus.Released, after.Status);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("visit_not_found", await TestData.ProblemCodeAsync(missing));
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.GateClerk)]
    [InlineData(Roles.YardPlanner)]
    [InlineData(Roles.BillingOfficer)]
    public async Task Every_role_can_read_visits(string role)
    {
        var client = await Factory.CreateClientAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/visits")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/visits/999999")).StatusCode);
    }
}
