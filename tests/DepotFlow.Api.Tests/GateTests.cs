using System.Net;
using System.Net.Http.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Common;
using DepotFlow.Application.Containers;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Security;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Api.Tests;

// Covers spec S1-05 to S1-09 (container number rules, gate in, gate out, lookup).
[Collection(ApiCollection.Name)]
public class GateTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string GateIn = "/api/v1/visits/gate-in";

    private static string GateOut(long visitId) => $"/api/v1/visits/{visitId}/gate-out";

    [Fact]
    public async Task S1_T_GATE_1_gate_in_a_valid_new_container_creates_container_and_visit()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);
        var number = TestData.NewContainerNumber();

        var response = await client.PostAsJsonAsync(GateIn, TestData.GateInBody(number));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var visit = await TestData.ReadAsync<VisitDto>(response);
        Assert.Equal(VisitStatus.InYard, visit.Status);
        Assert.Equal(number, visit.ContainerNumber);
        Assert.NotNull(response.Headers.Location);

        var found = await TestData.ReadAsync<PagedResult<ContainerListItemDto>>(
            await client.GetAsync($"/api/v1/containers?number={number}"));
        Assert.Single(found.Items);
    }

    [Fact]
    public async Task S1_T_GATE_2_invalid_check_digit_is_422()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);
        var valid = TestData.NewContainerNumber();
        var wrongDigit = valid[..10] + (valid[10] == '0' ? '1' : '0');

        var response = await client.PostAsJsonAsync(GateIn, TestData.GateInBody(wrongDigit));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_container_number", await TestData.ProblemCodeAsync(response));
    }

    [Fact]
    public async Task S1_T_GATE_3_gate_in_the_same_container_twice_is_409()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);
        var body = TestData.GateInBody(TestData.NewContainerNumber());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(GateIn, body)).StatusCode);

        var second = await client.PostAsJsonAsync(GateIn, body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("container_already_in_yard", await TestData.ProblemCodeAsync(second));
    }

    [Fact]
    public async Task S1_T_GATE_4_gate_in_out_in_again_gives_two_visits()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);
        var body = TestData.GateInBody(TestData.NewContainerNumber());

        var first = await TestData.ReadAsync<VisitDto>(await client.PostAsJsonAsync(GateIn, body));
        var released = await client.PostAsJsonAsync(GateOut(first.Id), TestData.GateOutBody());
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        Assert.Equal(1, (await TestData.ReadAsync<VisitDto>(released)).DwellDays);

        var secondResponse = await client.PostAsJsonAsync(GateIn, body);

        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var container = await TestData.ReadAsync<ContainerDetailDto>(
            await client.GetAsync($"/api/v1/containers/{first.ContainerId}"));
        Assert.Equal(2, container.Visits.Count);
        Assert.Equal(VisitStatus.InYard, container.Visits[0].Status);   // newest first
        Assert.Equal(VisitStatus.Released, container.Visits[1].Status);
    }

    [Fact]
    public async Task S1_T_GATE_5_different_size_after_first_visit_closed_is_409()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);
        var number = TestData.NewContainerNumber();
        var first = await TestData.ReadAsync<VisitDto>(
            await client.PostAsJsonAsync(GateIn, TestData.GateInBody(number, sizeFeet: 20)));
        await client.PostAsJsonAsync(GateOut(first.Id), TestData.GateOutBody());

        var response = await client.PostAsJsonAsync(GateIn, TestData.GateInBody(number, sizeFeet: 40));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("container_size_mismatch", await TestData.ProblemCodeAsync(response));
    }

    [Fact]
    public async Task S1_T_GATE_6_gate_out_an_already_released_visit_is_409()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);
        var visit = await TestData.ReadAsync<VisitDto>(
            await client.PostAsJsonAsync(GateIn, TestData.GateInBody(TestData.NewContainerNumber())));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(GateOut(visit.Id), TestData.GateOutBody())).StatusCode);

        var second = await client.PostAsJsonAsync(GateOut(visit.Id), TestData.GateOutBody());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("visit_already_released", await TestData.ProblemCodeAsync(second));
    }

    // Two requests race to gate in a container that has never been seen. The unique index on
    // Containers.Number decides the winner; the loser must get a clean 409, not a 500.
    [Fact]
    public async Task S1_T_GATE_7_simultaneous_gate_ins_of_a_new_container_give_one_201_and_one_409()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);

        for (var round = 0; round < 5; round++)
        {
            var body = TestData.GateInBody(TestData.NewContainerNumber());

            var responses = await Task.WhenAll(client.PostAsJsonAsync(GateIn, body), client.PostAsJsonAsync(GateIn, body));

            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(r => r.StatusCode).OrderBy(s => s));
        }
    }

    // Same race, but the container already exists and its last visit is closed. Now the filtered unique
    // index UX_Visits_Container_Active is the only thing that can stop two active visits.
    [Fact]
    public async Task S1_T_GATE_7_simultaneous_gate_ins_of_a_known_container_give_one_201_and_one_409()
    {
        var client = await Factory.CreateClientAsync(Roles.GateClerk);

        for (var round = 0; round < 5; round++)
        {
            var body = TestData.GateInBody(TestData.NewContainerNumber());
            var first = await TestData.ReadAsync<VisitDto>(await client.PostAsJsonAsync(GateIn, body));
            await client.PostAsJsonAsync(GateOut(first.Id), TestData.GateOutBody());

            var responses = await Task.WhenAll(client.PostAsJsonAsync(GateIn, body), client.PostAsJsonAsync(GateIn, body));

            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(r => r.StatusCode).OrderBy(s => s));
            Assert.All(
                responses.Where(r => r.StatusCode == HttpStatusCode.Conflict),
                r => Assert.Equal("container_already_in_yard", TestData.ProblemCodeAsync(r).Result));
        }
    }
}
