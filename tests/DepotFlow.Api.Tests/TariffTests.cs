using System.Net;
using System.Net.Http.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Security;
using DepotFlow.Application.Tariffs;

namespace DepotFlow.Api.Tests;

// Covers spec S2-05 (tariffs). Shipping line 5 is used so no tariff exists for it after a reset.
[Collection(ApiCollection.Name)]
public class TariffTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const int Line = 5;

    private static string Url(int lineId = Line) => $"/api/v1/shipping-lines/{lineId}/tariffs";

    [Fact]
    public async Task S2_T_TARIFF_valid_tiered_tariff_is_created()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);

        var response = await admin.PostAsJsonAsync(Url(), TestData.TieredTariffBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var tariff = await TestData.ReadAsync<TariffDto>(response);
        Assert.True(tariff.IsActive);
        Assert.Equal("BDT", tariff.Currency);
        Assert.Equal([5, 11], tariff.Tiers.Select(t => t.FromDay));
        Assert.Equal(400m, tariff.Tiers[1].RatePerDay);
    }

    [Fact]
    public async Task S2_T_TARIFF_gap_between_tiers_is_422_invalid_tariff()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var gap = new
        {
            sizeFeet = 20, strategyKey = "Tiered", freeDays = 4,
            tiers = new object[]
            {
                new { fromDay = 5, toDay = (int?)10, ratePerDay = 200m },
                new { fromDay = 12, toDay = (int?)null, ratePerDay = 400m }
            }
        };

        var response = await admin.PostAsJsonAsync(Url(), gap);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_tariff", await TestData.ProblemCodeAsync(response));
        Assert.Contains("gaps", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task S2_T_TARIFF_missing_tiers_is_400()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);

        var response = await admin.PostAsJsonAsync(Url(), new { sizeFeet = 20, strategyKey = "Tiered", freeDays = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await TestData.ProblemCodeAsync(response));
    }

    [Fact]
    public async Task S2_T_TARIFF_unknown_shipping_line_is_404()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);

        var response = await admin.PostAsJsonAsync(Url(999), TestData.TieredTariffBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task S2_T_TARIFF_second_create_deactivates_the_first()
    {
        var billing = await Factory.CreateClientAsync(Roles.BillingOfficer);
        var first = await TestData.ReadAsync<TariffDto>(await billing.PostAsJsonAsync(Url(), TestData.TieredTariffBody()));

        var secondResponse = await billing.PostAsJsonAsync(Url(), TestData.TieredTariffBody());
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await TestData.ReadAsync<TariffDto>(secondResponse);

        var all = await TestData.ReadAsync<List<TariffDto>>(await billing.GetAsync(Url()));
        Assert.Equal(2, all.Count);
        Assert.False(all.Single(t => t.Id == first.Id).IsActive);
        Assert.True(all.Single(t => t.Id == second.Id).IsActive);
    }

    [Fact]
    public async Task S2_T_TARIFF_other_size_is_independent()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        await admin.PostAsJsonAsync(Url(), TestData.TieredTariffBody(sizeFeet: 20));

        await admin.PostAsJsonAsync(Url(), TestData.TieredTariffBody(sizeFeet: 40));

        var all = await TestData.ReadAsync<List<TariffDto>>(await admin.GetAsync(Url()));
        Assert.Equal(2, all.Count(t => t.IsActive));
    }

    [Fact]
    public async Task S2_T_TARIFF_gate_clerk_gets_403_on_every_tariff_endpoint()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);

        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync(Url())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.PostAsJsonAsync(Url(), TestData.TieredTariffBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.PutAsJsonAsync("/api/v1/tariffs/1", new { isActive = false })).StatusCode);
    }

    [Fact]
    public async Task S2_T_TARIFF_can_be_deactivated_but_not_reactivated_through_put()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var tariff = await TestData.ReadAsync<TariffDto>(await admin.PostAsJsonAsync(Url(), TestData.TieredTariffBody()));

        var off = await admin.PutAsJsonAsync($"/api/v1/tariffs/{tariff.Id}", new { isActive = false });
        var on = await admin.PutAsJsonAsync($"/api/v1/tariffs/{tariff.Id}", new { isActive = true });
        var missing = await admin.PutAsJsonAsync("/api/v1/tariffs/999999", new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await TestData.ReadAsync<TariffDto>(off)).IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, on.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // Two creates for the same line and size at once. Whatever the interleaving, nobody gets a 500
    // and exactly one tariff ends up active (the filtered unique index guarantees it).
    [Fact]
    public async Task S2_T_TARIFF_simultaneous_creates_leave_exactly_one_active()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);

        for (var round = 0; round < 5; round++)
        {
            var responses = await Task.WhenAll(
                admin.PostAsJsonAsync(Url(), TestData.TieredTariffBody()),
                admin.PostAsJsonAsync(Url(), TestData.TieredTariffBody()));

            Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
            Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);

            var all = await TestData.ReadAsync<List<TariffDto>>(await admin.GetAsync(Url()));
            Assert.Equal(1, all.Count(t => t.IsActive && t.SizeFeet == 20));
        }
    }
}
