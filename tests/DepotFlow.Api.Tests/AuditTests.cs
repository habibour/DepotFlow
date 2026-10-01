using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DepotFlow.Api.Tests.Support;
using DepotFlow.Application.Audit;
using DepotFlow.Application.Common;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Security;
using DepotFlow.Application.ShippingLines;
using DepotFlow.Application.Tariffs;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Api.Tests;

// Covers spec S2-12 and S2-13 (audit log). The audit table is append-only, so tests never clear it:
// they look rows up by entity name and key instead.
[Collection(ApiCollection.Name)]
public class AuditTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static async Task<List<AuditLogDto>> RowsAsync(HttpClient admin, string entityName, object entityKey) =>
        (await TestData.ReadAsync<PagedResult<AuditLogDto>>(
            await admin.GetAsync($"/api/v1/audit-logs?entityName={entityName}&entityKey={entityKey}&pageSize=100"))).Items.ToList();

    private static string[] Names(JsonElement? values) => values!.Value.EnumerateObject().Select(p => p.Name).Order().ToArray();

    private static string NewLineCode() => "A" + Random.Shared.Next(100000, 999999);

    private static async Task<ShippingLineDto> CreateLineAsync(HttpClient admin, string code, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/shipping-lines", new { code, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestData.ReadAsync<ShippingLineDto>(response);
    }

    [Fact]
    public async Task S2_T_AUD_1_creating_a_shipping_line_writes_an_insert_row_with_the_user_and_new_values()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var code = NewLineCode();

        var line = await CreateLineAsync(admin, code, "Audit Test Line");

        var row = Assert.Single(await RowsAsync(admin, "ShippingLine", line.Id));
        Assert.Equal(AuditAction.Insert, row.Action);
        Assert.Equal("admin@depotflow.local", row.UserEmail);
        Assert.NotNull(row.UserId);
        Assert.Null(row.OldValues);
        Assert.Equal(code, row.NewValues!.Value.GetProperty("code").GetString());
        Assert.Equal("Audit Test Line", row.NewValues.Value.GetProperty("name").GetString());
        Assert.Equal(line.Id.ToString(), row.EntityKey);
    }

    [Fact]
    public async Task S2_T_AUD_2_updating_a_name_records_only_the_changed_field_with_old_and_new_values()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var line = await CreateLineAsync(admin, NewLineCode(), "Old Name");

        var update = await admin.PutAsJsonAsync($"/api/v1/shipping-lines/{line.Id}", new { name = "New Name", isActive = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var rows = await RowsAsync(admin, "ShippingLine", line.Id);
        Assert.Equal(2, rows.Count);
        var updateRow = rows[0];   // newest first
        Assert.Equal(AuditAction.Update, updateRow.Action);
        Assert.Equal(["name"], Names(updateRow.OldValues));
        Assert.Equal(["name"], Names(updateRow.NewValues));
        Assert.Equal("Old Name", updateRow.OldValues!.Value.GetProperty("name").GetString());
        Assert.Equal("New Name", updateRow.NewValues!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task S2_T_AUD_2_saving_without_a_real_change_writes_no_audit_row()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var line = await CreateLineAsync(admin, NewLineCode(), "Same Name");

        await admin.PutAsJsonAsync($"/api/v1/shipping-lines/{line.Id}", new { name = "Same Name", isActive = true });

        Assert.Single(await RowsAsync(admin, "ShippingLine", line.Id));   // just the insert
    }

    [Fact]
    public async Task S2_T_AUD_3_gate_out_audits_the_visit_update_and_the_invoice_insert_for_the_same_user()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var gateIn = await TestData.ReadAsync<VisitDto>(await clerk.PostAsJsonAsync("/api/v1/visits/gate-in", TestData.GateInBody(TestData.NewContainerNumber())));
        var released = await TestData.ReadAsync<VisitDto>(await clerk.PostAsJsonAsync($"/api/v1/visits/{gateIn.Id}/gate-out", TestData.GateOutBody()));

        var visitRows = await RowsAsync(admin, "Visit", gateIn.Id);
        var invoiceRows = await RowsAsync(admin, "Invoice", released.Invoice!.Id);

        Assert.Equal([AuditAction.Update, AuditAction.Insert], visitRows.Select(r => r.Action));   // newest first
        var visitUpdate = visitRows[0];
        Assert.Equal("InYard", visitUpdate.OldValues!.Value.GetProperty("status").GetString());
        Assert.Equal("Released", visitUpdate.NewValues!.Value.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, visitUpdate.NewValues.Value.GetProperty("yardSlotId").ValueKind);   // the slot was freed
        var invoiceInsert = Assert.Single(invoiceRows);
        Assert.Equal(AuditAction.Insert, invoiceInsert.Action);
        Assert.Matches(@"^INV-\d{4}-\d{7}$", invoiceInsert.NewValues!.Value.GetProperty("invoiceNumber").GetString()!);   // generated by the database
        Assert.Equal(visitUpdate.UserId, invoiceInsert.UserId);
        Assert.Equal("gate@depotflow.local", invoiceInsert.UserEmail);
    }

    [Fact]
    public async Task S2_T_AUD_3_gate_in_audits_the_new_container_and_visit()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var admin = await Factory.CreateClientAsync(Roles.Admin);

        var visit = await TestData.ReadAsync<VisitDto>(await clerk.PostAsJsonAsync("/api/v1/visits/gate-in", TestData.GateInBody(TestData.NewContainerNumber())));

        Assert.Equal(AuditAction.Insert, Assert.Single(await RowsAsync(admin, "Visit", visit.Id)).Action);
        Assert.Equal(AuditAction.Insert, Assert.Single(await RowsAsync(admin, "Container", visit.ContainerId)).Action);
    }

    [Fact]
    public async Task S2_T_AUD_4_updating_or_deleting_audit_rows_with_raw_sql_is_refused_by_the_database()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        await CreateLineAsync(admin, NewLineCode(), "Tamper Target");   // make sure at least one row exists

        var update = await Assert.ThrowsAnyAsync<Exception>(() => Factory.ExecuteSqlAsync("UPDATE AuditLogs SET UserEmail = 'someone.else@example.com'"));
        var delete = await Assert.ThrowsAnyAsync<Exception>(() => Factory.ExecuteSqlAsync("DELETE FROM AuditLogs"));

        Assert.Contains("append-only", update.Message);
        Assert.Contains("append-only", delete.Message);
    }

    [Theory]
    [InlineData(Roles.GateClerk)]
    [InlineData(Roles.YardPlanner)]
    [InlineData(Roles.BillingOfficer)]
    public async Task S2_T_AUD_5_non_admins_get_403(string role)
    {
        var client = await Factory.CreateClientAsync(role);

        var response = await client.GetAsync("/api/v1/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_save_that_fails_leaves_no_audit_rows_behind()
    {
        var clerk = await Factory.CreateClientAsync(Roles.GateClerk);
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var visit = await TestData.ReadAsync<VisitDto>(await clerk.PostAsJsonAsync("/api/v1/visits/gate-in", TestData.GateInBody(TestData.NewContainerNumber())));
        await Factory.InsertInvoiceForVisitAsync(visit.Id);   // makes the gate-out's invoice insert fail

        var response = await clerk.PostAsJsonAsync($"/api/v1/visits/{visit.Id}/gate-out", TestData.GateOutBody());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rows = await RowsAsync(admin, "Visit", visit.Id);
        Assert.Equal(AuditAction.Insert, Assert.Single(rows).Action);   // no Update row: that change was rolled back
    }

    [Fact]
    public async Task Creating_a_second_tariff_audits_the_deactivation_and_both_inserts()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var first = await TestData.ReadAsync<TariffDto>(await admin.PostAsJsonAsync("/api/v1/shipping-lines/6/tariffs", TestData.TieredTariffBody()));
        var second = await TestData.ReadAsync<TariffDto>(await admin.PostAsJsonAsync("/api/v1/shipping-lines/6/tariffs", TestData.TieredTariffBody()));

        var firstRows = await RowsAsync(admin, "Tariff", first.Id);
        var secondRows = await RowsAsync(admin, "Tariff", second.Id);

        Assert.Equal([AuditAction.Update, AuditAction.Insert], firstRows.Select(r => r.Action));
        Assert.True(firstRows[0].OldValues!.Value.GetProperty("isActive").GetBoolean());
        Assert.False(firstRows[0].NewValues!.Value.GetProperty("isActive").GetBoolean());
        Assert.Equal(AuditAction.Insert, Assert.Single(secondRows).Action);

        // The tiers of both tariffs are audited too (2 tiers each).
        var tierInserts = (await TestData.ReadAsync<PagedResult<AuditLogDto>>(
            await admin.GetAsync("/api/v1/audit-logs?entityName=TariffTier&pageSize=100"))).Items
            .Where(r => r.Action == AuditAction.Insert && r.NewValues!.Value.GetProperty("tariffId").GetInt32() is var id && (id == first.Id || id == second.Id));
        Assert.Equal(4, tierInserts.Count());
    }

    [Fact]
    public async Task Startup_seeding_is_audited_as_a_system_action_with_no_user()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);

        var rows = (await TestData.ReadAsync<PagedResult<AuditLogDto>>(
            await admin.GetAsync("/api/v1/audit-logs?entityName=ShippingLine&pageSize=100"))).Items;

        var seeded = rows.Where(r => r.UserId is null && r.UserEmail is null && r.Action == AuditAction.Insert).ToList();
        Assert.True(seeded.Count >= 8, $"expected the 8 seeded shipping lines, found {seeded.Count}");
    }

    [Fact]
    public async Task Audit_listing_is_newest_first_and_can_be_filtered_by_user_and_time()
    {
        var admin = await Factory.CreateClientAsync(Roles.Admin);
        var line = await CreateLineAsync(admin, NewLineCode(), "Filter Line");
        await admin.PutAsJsonAsync($"/api/v1/shipping-lines/{line.Id}", new { name = "Filter Line 2", isActive = true });
        var userId = (await RowsAsync(admin, "ShippingLine", line.Id))[0].UserId!;

        var byUser = (await TestData.ReadAsync<PagedResult<AuditLogDto>>(
            await admin.GetAsync($"/api/v1/audit-logs?userId={userId}&entityName=ShippingLine&pageSize=100"))).Items;

        // Other tests move the fake clock, so anchor the time filter on the newest row's own timestamp.
        var newest = (await TestData.ReadAsync<PagedResult<AuditLogDto>>(await admin.GetAsync("/api/v1/audit-logs?pageSize=1"))).Items.Single();
        async Task<int> CountFromAsync(DateTime from) => (await TestData.ReadAsync<PagedResult<AuditLogDto>>(
            await admin.GetAsync($"/api/v1/audit-logs?from={Uri.EscapeDataString(from.ToString("O"))}"))).TotalCount;

        Assert.All(byUser, r => Assert.Equal(userId, r.UserId));
        Assert.Equal(byUser.OrderByDescending(r => r.OccurredAtUtc).ThenByDescending(r => r.Id).Select(r => r.Id), byUser.Select(r => r.Id));
        Assert.Equal(0, await CountFromAsync(newest.OccurredAtUtc.AddSeconds(1)));   // nothing after the newest row
        Assert.True(await CountFromAsync(newest.OccurredAtUtc) >= 1);               // the newest row itself is included
    }
}
