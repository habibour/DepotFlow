using System.Net;
using System.Net.Http.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Security;

namespace DepotFlow.Api.Tests;

// Covers spec S1-10 and S2-15: the authorization matrices of Day 1 and Day 2, every endpoint against every role.
// Allowed roles must get past authorization (any status except 401 and 403); everyone else must get exactly 403.
// Requests are chosen so they change nothing: empty bodies give 400 and unknown ids give 404.
[Collection(ApiCollection.Name)]
public class AuthorizationMatrixTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly string[] Everyone = Roles.All;
    private static readonly string[] AdminOnly = [Roles.Admin];
    private static readonly string[] Gate = [Roles.Admin, Roles.GateClerk];
    private static readonly string[] Yard = [Roles.Admin, Roles.GateClerk, Roles.YardPlanner];
    private static readonly string[] YardEdit = [Roles.Admin, Roles.YardPlanner];
    private static readonly string[] Billing = [Roles.Admin, Roles.BillingOfficer];

    private static readonly object EmptyBody = new { };

    private sealed record Endpoint(HttpMethod Method, string Url, string[] Allowed, object? Body = null);

    private static readonly Endpoint[] Matrix =
    [
        // Day 1
        new(HttpMethod.Get, "/api/v1/shipping-lines", Everyone),
        new(HttpMethod.Get, "/api/v1/shipping-lines/999999", Everyone),
        new(HttpMethod.Post, "/api/v1/shipping-lines", AdminOnly, EmptyBody),
        new(HttpMethod.Put, "/api/v1/shipping-lines/999999", AdminOnly, new { name = "Nobody", isActive = true }),
        new(HttpMethod.Post, "/api/v1/visits/gate-in", Gate, EmptyBody),
        new(HttpMethod.Post, "/api/v1/visits/999999/gate-out", Gate, new { truckNumber = "T1" }),
        new(HttpMethod.Get, "/api/v1/containers", Everyone),
        new(HttpMethod.Get, "/api/v1/containers/999999", Everyone),

        // Day 2
        new(HttpMethod.Post, "/api/v1/visits/999999/relocate", YardEdit, new { targetSlotId = 1 }),
        new(HttpMethod.Get, "/api/v1/yard/slots", Yard),
        new(HttpMethod.Get, "/api/v1/yard/occupancy", Yard),
        new(HttpMethod.Get, "/api/v1/shipping-lines/1/tariffs", Billing),
        new(HttpMethod.Post, "/api/v1/shipping-lines/1/tariffs", Billing, EmptyBody),
        new(HttpMethod.Put, "/api/v1/tariffs/999999", Billing, new { isActive = false }),
        new(HttpMethod.Get, "/api/v1/visits/999999/charge-preview", Billing),
        new(HttpMethod.Get, "/api/v1/invoices", Billing),
        new(HttpMethod.Get, "/api/v1/invoices/999999", Billing),
        new(HttpMethod.Post, "/api/v1/invoices/999999/pay", Billing),
        new(HttpMethod.Get, "/api/v1/audit-logs", AdminOnly),
        new(HttpMethod.Get, "/api/v1/visits", Everyone),
        new(HttpMethod.Get, "/api/v1/visits/999999", Everyone)
    ];

    [Fact]
    public async Task Every_endpoint_allows_exactly_the_roles_in_the_matrix()
    {
        var clients = new Dictionary<string, HttpClient>();
        foreach (var role in Roles.All)
        {
            clients[role] = await Factory.CreateClientAsync(role);
        }

        var wrong = new List<string>();
        foreach (var endpoint in Matrix)
        {
            foreach (var role in Roles.All)
            {
                var request = new HttpRequestMessage(endpoint.Method, endpoint.Url);
                if (endpoint.Body is not null)
                {
                    request.Content = JsonContent.Create(endpoint.Body);
                }

                var status = (await clients[role].SendAsync(request)).StatusCode;
                var allowed = endpoint.Allowed.Contains(role);
                var passedAuthorization = status is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

                if (allowed != passedAuthorization)
                {
                    wrong.Add($"{endpoint.Method} {endpoint.Url} as {role}: got {(int)status}, expected {(allowed ? "anything but 401/403" : "403")}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public async Task Every_endpoint_rejects_a_missing_token_with_401_except_login_and_health()
    {
        var anonymous = Factory.CreateClient();

        var wrong = new List<string>();
        foreach (var endpoint in Matrix)
        {
            var request = new HttpRequestMessage(endpoint.Method, endpoint.Url);
            if (endpoint.Body is not null)
            {
                request.Content = JsonContent.Create(endpoint.Body);
            }

            var status = (await anonymous.SendAsync(request)).StatusCode;
            if (status != HttpStatusCode.Unauthorized)
            {
                wrong.Add($"{endpoint.Method} {endpoint.Url} without a token: got {(int)status}, expected 401");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health")).StatusCode);

        // Login needs no token: a bad password is refused by the login itself (401 with a problem body), not by the gate.
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = "gate@depotflow.local", password = "wrong" });
        Assert.Equal("Invalid credentials", System.Text.Json.JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("title").GetString());
    }
}
