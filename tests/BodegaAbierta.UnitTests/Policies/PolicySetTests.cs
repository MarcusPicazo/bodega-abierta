using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.UnitTests.Policies;

public sealed class PolicySetTests
{
    private static readonly Guid FoodBankId = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 10, 0, 0, TimeSpan.FromHours(-6));

    private static readonly RuleDefinition Fefo = new(RuleType.FefoAllocation, "{}");
    private static readonly RuleDefinition Alert = new(RuleType.ExpiryAlert, """{"daysBeforeExpiry":3}""");

    private static PolicySet Draft(int version = 1, params RuleDefinition[] rules)
    {
        var policy = new PolicySet(FoodBankId, version, "Reglas de operación", "ana", T0);
        policy.Edit(policy.Description, rules.Length == 0 ? [Fefo] : rules, T0);
        return policy;
    }

    private static PolicySet Pending(int version = 1)
    {
        var policy = Draft(version);
        policy.Submit("ana", T0.AddMinutes(1));
        return policy;
    }

    private static PolicySet Active(int version = 1)
    {
        var policy = Pending(version);
        policy.Approve("coordinadora", T0.AddMinutes(2));
        return policy;
    }

    private static void ShouldViolate(Action act, string code)
        => act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(code);

    [Theory]
    [InlineData(PolicySetStatus.Draft, PolicySetStatus.PendingApproval, true)]
    [InlineData(PolicySetStatus.PendingApproval, PolicySetStatus.Active, true)]
    [InlineData(PolicySetStatus.PendingApproval, PolicySetStatus.Draft, true)]
    [InlineData(PolicySetStatus.Active, PolicySetStatus.Retired, true)]
    [InlineData(PolicySetStatus.Draft, PolicySetStatus.Active, false)]
    [InlineData(PolicySetStatus.Active, PolicySetStatus.Draft, false)]
    [InlineData(PolicySetStatus.Retired, PolicySetStatus.Active, false)]
    [InlineData(PolicySetStatus.Retired, PolicySetStatus.Draft, false)]
    public void Transition_table(PolicySetStatus from, PolicySetStatus to, bool allowed)
        => PolicySet.CanTransition(from, to).Should().Be(allowed);

    [Fact]
    public void Happy_path_records_who_did_what_and_when()
    {
        var policy = Active();

        policy.Status.Should().Be(PolicySetStatus.Active);
        policy.SubmittedBy.Should().Be("ana");
        policy.ApprovedBy.Should().Be("coordinadora");
        policy.ApprovedAt.Should().Be(T0.AddMinutes(2));
        policy.LastModifiedAt.Should().Be(T0.AddMinutes(2));
    }

    [Fact]
    public void Edit_replaces_rules_and_bumps_LastModifiedAt()
    {
        var policy = Draft(1, Fefo);

        policy.Edit("v1 corregida", [Alert], T0.AddHours(1));

        policy.Rules.Should().ContainSingle().Which.Type.Should().Be(RuleType.ExpiryAlert);
        policy.LastModifiedAt.Should().Be(T0.AddHours(1));
        policy.Rules.Single().PolicySetId.Should().Be(policy.Id);
        policy.Rules.Single().FoodBankId.Should().Be(FoodBankId);
    }

    [Fact]
    public void Only_drafts_can_be_edited()
        => ShouldViolate(() => Pending().Edit("x", [Fefo], T0), "policy.not_draft");

    [Fact]
    public void Duplicate_rule_types_are_rejected()
        => ShouldViolate(() => Draft().Edit(null, [Fefo, Fefo], T0), "policy.duplicate_rule");

    [Fact]
    public void Cannot_submit_without_rules()
    {
        var empty = new PolicySet(FoodBankId, 1, null, "ana", T0);

        ShouldViolate(() => empty.Submit("ana", T0), "policy.no_rules");
    }

    [Fact]
    public void Submitter_cannot_approve_their_own_version()
        => ShouldViolate(() => Pending().Approve("ANA", T0), "policy.self_approval");

    [Fact]
    public void A_draft_cannot_be_approved_directly()
        => ShouldViolate(() => Draft().Approve("coordinadora", T0), "policy.invalid_transition");

    [Fact]
    public void Reject_sends_back_to_draft_with_reason_and_allows_resubmission()
    {
        var policy = Pending();

        policy.Reject("coordinadora", "Falta límite por beneficiario", T0.AddMinutes(5));

        policy.Status.Should().Be(PolicySetStatus.Draft);
        policy.RejectionReason.Should().Be("Falta límite por beneficiario");
        policy.SubmittedBy.Should().BeNull();

        policy.Submit("ana", T0.AddMinutes(6));
        policy.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Reject_requires_a_reason()
        => ShouldViolate(() => Pending().Reject("coordinadora", " ", T0), "policy.rejection_reason_required");

    [Fact]
    public void Retiring_records_the_replacement()
    {
        var v1 = Active(1);
        var v2 = Pending(2);

        v1.RetireInFavorOf(v2, T0.AddDays(1));

        v1.Status.Should().Be(PolicySetStatus.Retired);
        v1.RetiredAt.Should().Be(T0.AddDays(1));
        v1.ReplacedByPolicySetId.Should().Be(v2.Id);
    }

    [Fact]
    public void Retired_is_final()
    {
        var v1 = Active(1);
        v1.RetireInFavorOf(Pending(2), T0);

        ShouldViolate(() => v1.RetireInFavorOf(Pending(3), T0), "policy.invalid_transition");
    }

    private const string WithdrawTodo = "TODO(Marcus): implement PolicySet.Withdraw";

    [Fact(Skip = WithdrawTodo)]
    public void Withdraw_by_the_submitter_returns_to_draft_and_clears_submission()
    {
    }

    [Fact(Skip = WithdrawTodo)]
    public void Withdraw_by_someone_else_violates_policy_not_submitter()
    {
    }

    [Fact(Skip = WithdrawTodo)]
    public void Withdraw_from_draft_or_active_violates_policy_invalid_transition()
    {
    }
}
