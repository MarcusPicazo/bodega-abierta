using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Auditing;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Donors;
using BodegaAbierta.Application.Policies;
using BodegaAbierta.Domain.Auditing;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.IntegrationTests.Infrastructure;

namespace BodegaAbierta.IntegrationTests;

public sealed class AuditTrailTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private static async Task<IReadOnlyList<AuditEntryResponse>> HistoryAsync(HttpClient coordinator, string entityType, Guid id)
    {
        var page = await coordinator.GetFromJsonAsync<PagedResult<AuditEntryResponse>>(
            $"/api/v1/audit-entries?entityType={entityType}&entityId={id}", Json, Ct);
        return page!.Items;
    }

    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public async Task Create_and_update_are_audited_with_actor_and_before_after()
    {
        var tenant = await CreateTenantAsync();
        var staff = CreateClientAs(tenant, "ana");
        var coordinator = CreateClientAs(tenant, "coordinadora", Roles.Coordinator);

        var created = await ReadAsync<DonorResponse>(await PostAsync(staff, "/api/v1/donors",
            new CreateDonorRequest("Panadería San Juan", DonorType.Business, null, null)));
        var updated = await SendWithIfMatchAsync(staff, HttpMethod.Put, $"/api/v1/donors/{created.Id}", $"\"{created.Version}\"",
            new UpdateDonorRequest("Panadería San Juan Centro", DonorType.Business, null, null, IsActive: true));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await HistoryAsync(coordinator, nameof(Donor), created.Id);

        history.Should().HaveCount(2).And.OnlyContain(e => e.Actor == "ana");
        var update = history[0]; // newest first
        update.Action.Should().Be(AuditAction.Updated);
        Parse(update.BeforeJson).GetProperty("Name").GetString().Should().Be("Panadería San Juan");
        Parse(update.AfterJson).GetProperty("Name").GetString().Should().Be("Panadería San Juan Centro");
        Parse(update.AfterJson).TryGetProperty("Type", out _).Should().BeFalse("only changed properties are recorded");

        var create = history[1];
        create.Action.Should().Be(AuditAction.Created);
        create.BeforeJson.Should().BeNull();
        Parse(create.AfterJson).GetProperty("Type").GetString().Should().Be("Business");
    }

    [Fact]
    public async Task Approval_audits_the_status_change_and_rule_replacement()
    {
        var tenant = await CreateTenantAsync();
        var staff = CreateClientAs(tenant, "ana");
        var coordinator = CreateClientAs(tenant, "coordinadora", Roles.Coordinator);
        var empty = JsonDocument.Parse("{}").RootElement;

        var draft = await ReadAsync<PolicySetResponse>(await PostAsync(staff, "/api/v1/policies",
            new CreatePolicySetRequest(null, [new(RuleType.FefoAllocation, empty)], null)));
        var edited = await ReadAsync<PolicySetResponse>(await SendWithIfMatchAsync(staff, HttpMethod.Put,
            $"/api/v1/policies/{draft.Id}", $"\"{draft.RowVersion}\"",
            new UpdatePolicySetRequest(null, [new(RuleType.ExpiryAlert, JsonDocument.Parse("""{"daysBeforeExpiry":3}""").RootElement)])));
        var pending = await ReadAsync<PolicySetResponse>(await SendWithIfMatchAsync(staff, HttpMethod.Post,
            $"/api/v1/policies/{edited.Id}/submit", $"\"{edited.RowVersion}\""));
        (await SendWithIfMatchAsync(coordinator, HttpMethod.Post, $"/api/v1/policies/{pending.Id}/approve", $"\"{pending.RowVersion}\""))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var setHistory = await HistoryAsync(coordinator, nameof(PolicySet), draft.Id);
        var approval = setHistory[0];
        approval.Actor.Should().Be("coordinadora");
        Parse(approval.BeforeJson).GetProperty("Status").GetString().Should().Be("PendingApproval");
        Parse(approval.AfterJson).GetProperty("Status").GetString().Should().Be("Active");

        // Replacing the rules deleted the FEFO rule row and created the ExpiryAlert one: both audited.
        var fefoRuleId = draft.Rules.Single().Id;
        var ruleHistory = await HistoryAsync(coordinator, nameof(PolicyRule), fefoRuleId);
        ruleHistory.Select(e => e.Action).Should().Equal(AuditAction.Deleted, AuditAction.Created);
    }

    [Fact]
    public async Task Audit_log_is_for_coordinators_only()
    {
        var tenant = await CreateTenantAsync();

        var response = await CreateClientAs(tenant, "ana").GetAsync(new Uri("/api/v1/audit-entries", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Skip = "TODO(Marcus): mask Donor.Email/Phone with [AuditMasked] in AuditSaveChangesInterceptor")]
    public async Task Donor_contact_data_is_masked_in_the_audit_log()
    {
        var tenant = await CreateTenantAsync();
        var staff = CreateClientAs(tenant, "ana");
        var coordinator = CreateClientAs(tenant, "coordinadora", Roles.Coordinator);

        var donor = await ReadAsync<DonorResponse>(await PostAsync(staff, "/api/v1/donors",
            new CreateDonorRequest("Luisa Pérez", DonorType.Individual, "luisa@example.org", "55 1111 2222")));

        var created = (await HistoryAsync(coordinator, nameof(Donor), donor.Id)).Single();
        var after = Parse(created.AfterJson);
        after.GetProperty("Email").GetString().Should().Be("***");
        after.GetProperty("Phone").GetString().Should().Be("***");
        after.GetProperty("Name").GetString().Should().Be("Luisa Pérez");
    }
}
