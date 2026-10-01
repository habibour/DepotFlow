using System.Net;
using System.Net.Http.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Common;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Security;
using DepotFlow.Application.Yard;

namespace DepotFlow.Api.Tests;

// Covers spec S2-02, S2-03, S2-04, S2-07 (slots, relocation, yard read endpoints, tariff rule at gate-in).
// The test yard has 4 slots: A-01-01-1, A-01-01-2, A-01-02-1, A-01-02-2.
[Collection(ApiCollection.Name)]
public class YardTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string GateIn = "/api/v1/visits/gate-in";

    private static async Task<VisitDto> GateInAsync(HttpClient client, int sizeFeet = 20, int lineId = 1)
    {
        var response = await client.PostAsJsonAsync(GateIn, new
        {
            containerNumber = TestData.NewContainerNumber(),
            sizeFeet,
            shippingLineId = lineId,
            truckNumber = "DHK-TA-11-2345",
            sealNumber = "SL889201"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestData.ReadAsync<VisitDto>(response);
    }

    private static async Task<HttpResponseMessage> RelocateAsync(HttpClient client, long visitId, int targetSlotId) =>
        await client.PostAsJsonAsync($"/api/v1/visits/{visitId}/relocate", new { targetSlotId });

    private static async Task<List<YardSlotDto>> SlotsAsync(HttpClient client) =>
        (await TestData.ReadAsync<PagedResult<YardSlotDto>>(await client.GetAsync("/api/v1/yard/slots?pageSize=200"))).Items.ToList();

    private static async Task<int> SlotIdAsync(HttpClient client, string code) =>
        (await SlotsAsync(client)).Single(s => s.Code == code).Id;

    [Fact]
    public async Task S2_T_YARD_1_two_gate_ins_fill_tier_1_then_tier_2_of_the_same_stack()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);

        var first = await GateInAsync(clerk);
        var second = await GateInAsync(clerk);

        Assert.Equal("A-01-01-1", first.YardSlot!.Code);
        Assert.Equal("A-01-01-2", second.YardSlot!.Code);
    }

    [Fact]
    public async Task S2_T_YARD_2_more_containers_than_slots_gives_409_yard_full()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var codes = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            codes.Add((await GateInAsync(clerk)).YardSlot!.Code);
        }

        var extra = await clerk.PostAsJsonAsync(GateIn, TestData.GateInBody(TestData.NewContainerNumber()));

        Assert.Equal(["A-01-01-1", "A-01-01-2", "A-01-02-1", "A-01-02-2"], codes);
        Assert.Equal(HttpStatusCode.Conflict, extra.StatusCode);
        Assert.Equal("yard_full", await TestData.ProblemCodeAsync(extra));
    }

    // Two different containers arrive at once and both pick A-01-01-1. The slot index rejects the loser's save;
    // the use case then retries with fresh data, so BOTH must end up in the yard, in different slots.
    [Fact]
    public async Task S2_T_YARD_2_simultaneous_gate_ins_of_different_containers_both_succeed_in_different_slots()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);

        for (var round = 0; round < 3; round++)
        {
            var responses = await Task.WhenAll(
                clerk.PostAsJsonAsync(GateIn, TestData.GateInBody(TestData.NewContainerNumber())),
                clerk.PostAsJsonAsync(GateIn, TestData.GateInBody(TestData.NewContainerNumber())));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
            var visits = await Task.WhenAll(responses.Select(r => TestData.ReadAsync<VisitDto>(r)));
            Assert.Equal(2, visits.Select(v => v.YardSlot!.Code).Distinct().Count());

            foreach (var visit in visits)
            {
                await clerk.PostAsJsonAsync($"/api/v1/visits/{visit.Id}/gate-out", TestData.GateOutBody());
            }
        }
    }

    [Fact]
    public async Task S2_T_YARD_3_relocate_to_an_occupied_slot_gives_409_slot_occupied()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        await GateInAsync(clerk);
        var second = await GateInAsync(clerk);

        var response = await RelocateAsync(planner, second.Id, await SlotIdAsync(planner, "A-01-01-1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("slot_occupied", await TestData.ProblemCodeAsync(response));
    }

    [Fact]
    public async Task S2_T_YARD_4_relocate_to_tier_2_when_tier_1_is_empty_gives_422_stacking_rule_violated()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        var visit = await GateInAsync(clerk);   // A-01-01-1

        // Bay 2 is empty, so tier 2 there has nothing under it.
        var elsewhere = await RelocateAsync(planner, visit.Id, await SlotIdAsync(planner, "A-01-02-2"));
        // Tier 2 of its own stack: its own tier-1 slot is about to be vacated, so it cannot be the support.
        var ownStack = await RelocateAsync(planner, visit.Id, await SlotIdAsync(planner, "A-01-01-2"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, elsewhere.StatusCode);
        Assert.Equal("stacking_rule_violated", await TestData.ProblemCodeAsync(elsewhere));
        Assert.Equal("stacking_rule_violated", await TestData.ProblemCodeAsync(ownStack));
    }

    [Fact]
    public async Task S2_T_YARD_5_successful_relocate_frees_the_old_slot_and_occupies_the_new_one()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        var bottom = await GateInAsync(clerk);   // A-01-01-1
        var top = await GateInAsync(clerk);      // A-01-01-2
        await clerk.PostAsJsonAsync($"/api/v1/visits/{bottom.Id}/gate-out", TestData.GateOutBody());   // tier 1 is free again

        var response = await RelocateAsync(planner, top.Id, await SlotIdAsync(planner, "A-01-01-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("A-01-01-1", (await TestData.ReadAsync<VisitDto>(response)).YardSlot!.Code);
        var slots = await SlotsAsync(planner);
        Assert.Equal(top.Id, slots.Single(s => s.Code == "A-01-01-1").Occupant!.VisitId);
        Assert.Null(slots.Single(s => s.Code == "A-01-01-2").Occupant);
    }

    // Two visits fight over the one free, placeable slot (A-01-02-1). The filtered unique index on
    // Visits(YardSlotId) decides; the loser must get a clean 409, not a 500.
    [Fact]
    public async Task S2_T_YARD_6_two_simultaneous_relocations_to_the_same_slot_give_one_200_and_one_409()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        var target = await SlotIdAsync(planner, "A-01-02-1");

        for (var round = 0; round < 3; round++)
        {
            var first = await GateInAsync(clerk);    // A-01-01-1
            var second = await GateInAsync(clerk);   // A-01-01-2

            var responses = await Task.WhenAll(RelocateAsync(planner, first.Id, target), RelocateAsync(planner, second.Id, target));

            Assert.Equal(
                [HttpStatusCode.OK, HttpStatusCode.Conflict],
                responses.Select(r => r.StatusCode).OrderBy(s => s));
            var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("slot_occupied", await TestData.ProblemCodeAsync(loser));

            // Empty the yard for the next round.
            foreach (var visit in new[] { first, second })
            {
                await clerk.PostAsJsonAsync($"/api/v1/visits/{visit.Id}/gate-out", TestData.GateOutBody());
            }
        }
    }

    [Fact]
    public async Task Relocate_other_rules_same_slot_missing_slot_missing_visit_released_visit()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var planner = await Factory.CreateClientAsync(Roles.YardPlanner);
        var visit = await GateInAsync(clerk);   // A-01-01-1

        var same = await RelocateAsync(planner, visit.Id, visit.YardSlot!.Id);
        var noSlot = await RelocateAsync(planner, visit.Id, 999999);
        var noVisit = await RelocateAsync(planner, 999999, visit.YardSlot.Id);
        await clerk.PostAsJsonAsync($"/api/v1/visits/{visit.Id}/gate-out", TestData.GateOutBody());
        var released = await RelocateAsync(planner, visit.Id, await SlotIdAsync(planner, "A-01-02-1"));

        Assert.Equal("same_slot", await TestData.ProblemCodeAsync(same));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, same.StatusCode);
        Assert.Equal("slot_not_found", await TestData.ProblemCodeAsync(noSlot));
        Assert.Equal("visit_not_found", await TestData.ProblemCodeAsync(noVisit));
        Assert.Equal("visit_not_in_yard", await TestData.ProblemCodeAsync(released));
        Assert.Equal(HttpStatusCode.Conflict, released.StatusCode);
    }

    [Fact]
    public async Task Relocate_is_for_yard_planners_and_admins_only()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var visit = await GateInAsync(clerk);
        var target = await SlotIdAsync(admin, "A-01-02-1");

        Assert.Equal(HttpStatusCode.Forbidden, (await RelocateAsync(clerk, visit.Id, target)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RelocateAsync(billing, visit.Id, target)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RelocateAsync(admin, visit.Id, target)).StatusCode);
    }

    [Fact]
    public async Task Yard_endpoints_show_occupancy_and_respect_roles()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        await GateInAsync(clerk);
        await GateInAsync(clerk);

        var occupancy = await TestData.ReadAsync<List<BlockOccupancyDto>>(await clerk.GetAsync("/api/v1/yard/occupancy"));
        var occupiedOnly = await TestData.ReadAsync<PagedResult<YardSlotDto>>(await clerk.GetAsync("/api/v1/yard/slots?occupied=true"));

        var block = Assert.Single(occupancy);
        Assert.Equal((4, 2, 50m), (block.TotalSlots, block.OccupiedSlots, block.PercentOccupied));
        Assert.Equal(2, occupiedOnly.TotalCount);
        Assert.All(occupiedOnly.Items, s => Assert.NotNull(s.Occupant));
        Assert.Equal(HttpStatusCode.Forbidden, (await billing.GetAsync("/api/v1/yard/occupancy")).StatusCode);
    }

    [Fact]
    public async Task S2_T_BILL_1_gate_in_for_a_line_with_no_tariff_gives_422_no_active_tariff()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);

        // Shipping line 3 has no tariff in the test database.
        var response = await clerk.PostAsJsonAsync(GateIn, new
        {
            containerNumber = TestData.NewContainerNumber(),
            sizeFeet = 20,
            shippingLineId = 3,
            truckNumber = "T1",
            sealNumber = "S1"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("no_active_tariff", await TestData.ProblemCodeAsync(response));
    }
}
