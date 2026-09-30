using System.Net;
using System.Net.Http.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Auth;
using DepotFlow.Application.Security;

namespace DepotFlow.Api.Tests;

// Covers spec S1-03 (authentication) and S1-10 (authorization).
[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task S1_T_AUTH_1_login_with_correct_credentials_returns_a_token()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "gate@depotflow.local", password = ApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await TestData.ReadAsync<LoginResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(Roles.GateClerk, login.Role);
        Assert.True(login.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task S1_T_AUTH_2_login_with_wrong_password_is_401()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "gate@depotflow.local", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task S1_T_AUTH_3_gate_in_without_a_token_is_401()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/visits/gate-in",
            TestData.GateInBody(TestData.NewContainerNumber()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task S1_T_AUTH_4_billing_officer_cannot_gate_in()
    {
        var client = await Factory.CreateClientAsync(Roles.BillingOfficer);

        var response = await client.PostAsJsonAsync("/api/v1/visits/gate-in",
            TestData.GateInBody(TestData.NewContainerNumber()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
