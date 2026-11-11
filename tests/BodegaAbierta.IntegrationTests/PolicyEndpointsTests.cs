using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Policies;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.IntegrationTests;

public sealed class PolicyEndpointsTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private const string Url = "/api/v1/policies";

    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement;
    private static readonly JsonElement ThreeDays = JsonDocument.Parse("""{"daysBeforeExpiry":3}""").RootElement;

    private static CreatePolicySetRequest NewDraft(params PolicyRuleRequest[] rules)
        => new("Reglas de operación", rules.Length == 0 ? [new(RuleType.FefoAllocation, Empty)] : rules, null);

    private sealed record Actors(HttpClient Staff, HttpClient Coordinator, HttpClient OtherStaff);

    private Actors ActorsFor(TestTenant tenant) => new(
        CreateClientAs(tenant, "ana"),
        CreateClientAs(tenant, "coordinadora", Roles.Coordinator),
        CreateClientAs(tenant, "beto"));

    private static string ETagOf(PolicySetResponse policy) => $"\"{policy.RowVersion}\"";

    private static async Task<PolicySetResponse> CreateAsync(HttpClient client, CreatePolicySetRequest request)
    {
        var response = await PostAsync(client, Url, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<PolicySetResponse>(response);
    }

    private static async Task<PolicySetResponse> TransitionAsync(HttpClient client, PolicySetResponse policy, string action, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await SendWithIfMatchAsync(client, HttpMethod.Post, $"{Url}/{policy.Id}/{action}", ETagOf(policy));
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync(Ct));
        return expected == HttpStatusCode.OK ? await ReadAsync<PolicySetResponse>(response) : policy;
    }

    private static async Task<PolicySetResponse> ActivateAsync(Actors actors, CreatePolicySetRequest request)
    {
        var draft = await CreateAsync(actors.Staff, request);
        var pending = await TransitionAsync(actors.Staff, draft, "submit");
        return await TransitionAsync(actors.Coordinator, pending, "approve");
    }

    [Fact]
    public async Task Full_lifecycle_keeps_exactly_one_active_version()
    {
        var tenant = await CreateTenantAsync();
        var actors = ActorsFor(tenant);

        var v1 = await ActivateAsync(actors, NewDraft());
        v1.Version.Should().Be(1);
        v1.Status.Should().Be(PolicySetStatus.Active);
        v1.ApprovedBy.Should().Be("coordinadora");

        // v2 starts as a copy of v1, gets an extra rule, and replaces it.
        var v2Draft = await CreateAsync(actors.Staff, new CreatePolicySetRequest(null, null, v1.Id));
        v2Draft.Version.Should().Be(2);
        v2Draft.Rules.Select(r => r.Type).Should().Equal(RuleType.FefoAllocation);

        var edited = await SendWithIfMatchAsync(actors.Staff, HttpMethod.Put, $"{Url}/{v2Draft.Id}", ETagOf(v2Draft),
            new UpdatePolicySetRequest("v2 con alertas", [new(RuleType.FefoAllocation, Empty), new(RuleType.ExpiryAlert, ThreeDays)]));
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        var v2Edited = await ReadAsync<PolicySetResponse>(edited);

        var v2Pending = await TransitionAsync(actors.Staff, v2Edited, "submit");
        var v2 = await TransitionAsync(actors.Coordinator, v2Pending, "approve");
        v2.Status.Should().Be(PolicySetStatus.Active);

        var active = await actors.Staff.GetFromJsonAsync<PolicySetResponse>($"{Url}/active", Json, Ct);
        active!.Id.Should().Be(v2.Id);
        active.Rules.Single(r => r.Type == RuleType.ExpiryAlert).Parameters.GetProperty("daysBeforeExpiry").GetInt32().Should().Be(3);

        var retiredV1 = await actors.Staff.GetFromJsonAsync<PolicySetResponse>($"{Url}/{v1.Id}", Json, Ct);
        retiredV1!.Status.Should().Be(PolicySetStatus.Retired);
        retiredV1.ReplacedByPolicySetId.Should().Be(v2.Id);

        await using var db = Sql.CreateDbContext(tenant.FoodBankId);
        (await db.PolicySets.CountAsync(p => p.Status == PolicySetStatus.Active, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Submitter_cannot_approve_their_own_version_even_as_coordinator()
    {
        var tenant = await CreateTenantAsync();
        var coordinator = CreateClientAs(tenant, "coordinadora", Roles.Coordinator);
        var pending = await TransitionAsync(coordinator, await CreateAsync(coordinator, NewDraft()), "submit");

        var response = await SendWithIfMatchAsync(coordinator, HttpMethod.Post, $"{Url}/{pending.Id}/approve", ETagOf(pending));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("policy.self_approval");
    }

    [Fact]
    public async Task Approval_requires_the_coordinator_role()
    {
        var tenant = await CreateTenantAsync();
        var actors = ActorsFor(tenant);
        var pending = await TransitionAsync(actors.Staff, await CreateAsync(actors.Staff, NewDraft()), "submit");

        await TransitionAsync(actors.OtherStaff, pending, "approve", HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_requests_get_401_problem_details()
    {
        var tenant = await CreateTenantAsync(); // tenant header only, no user

        var response = await PostAsync(tenant.Client, Url, NewDraft());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Pending_version_cannot_be_edited()
    {
        var tenant = await CreateTenantAsync();
        var actors = ActorsFor(tenant);
        var pending = await TransitionAsync(actors.Staff, await CreateAsync(actors.Staff, NewDraft()), "submit");

        var response = await SendWithIfMatchAsync(actors.Staff, HttpMethod.Put, $"{Url}/{pending.Id}", ETagOf(pending),
            new UpdatePolicySetRequest("cambio tardío", [new(RuleType.FefoAllocation, Empty)]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("policy.not_draft");
    }

    [Fact]
    public async Task Rejected_version_goes_back_to_draft_with_the_reason()
    {
        var tenant = await CreateTenantAsync();
        var actors = ActorsFor(tenant);
        var pending = await TransitionAsync(actors.Staff, await CreateAsync(actors.Staff, NewDraft()), "submit");

        var response = await SendWithIfMatchAsync(actors.Coordinator, HttpMethod.Post, $"{Url}/{pending.Id}/reject", ETagOf(pending),
            new RejectPolicySetRequest("Agregar ventana de recolección"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await ReadAsync<PolicySetResponse>(response);
        rejected.Status.Should().Be(PolicySetStatus.Draft);
        rejected.RejectionReason.Should().Be("Agregar ventana de recolección");
    }

    [Fact]
    public async Task Stale_if_match_on_approve_returns_412()
    {
        var tenant = await CreateTenantAsync();
        var actors = ActorsFor(tenant);
        var draft = await CreateAsync(actors.Staff, NewDraft());
        await TransitionAsync(actors.Staff, draft, "submit"); // changes the version...

        // ...so approving with the draft's ETag is rejected.
        await TransitionAsync(actors.Coordinator, draft, "approve", HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Invalid_rules_return_400()
    {
        var tenant = await CreateTenantAsync();
        var staff = CreateClientAs(tenant, "ana");

        var response = await PostAsync(staff, Url, NewDraft(
            new PolicyRuleRequest(RuleType.FefoAllocation, Empty),
            new PolicyRuleRequest(RuleType.FefoAllocation, JsonDocument.Parse("[1,2]").RootElement)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().Contain("rules[1].parameters").And.Contain("Each rule type can appear only once.");
    }

    [Fact]
    public async Task Database_rejects_two_active_versions_for_the_same_food_bank()
    {
        var tenant = await CreateTenantAsync();
        var now = DateTimeOffset.UtcNow;

        // Bypass the service on purpose: activate two versions without retiring the first.
        PolicySet ActivePolicy(int version)
        {
            var policy = new PolicySet(tenant.FoodBankId, version, null, "ana", now);
            policy.Edit(null, [new RuleDefinition(RuleType.FefoAllocation, "{}")], now);
            policy.Submit("ana", now);
            policy.Approve("coordinadora", now);
            return policy;
        }

        await using var db = Sql.CreateDbContext(tenant.FoodBankId);
        db.PolicySets.AddRange(ActivePolicy(1), ActivePolicy(2));

        var act = () => db.SaveChangesAsync(Ct);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("UX_PolicySets_OneActivePerFoodBank");
    }
}
