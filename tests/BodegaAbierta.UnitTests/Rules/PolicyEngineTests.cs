using BodegaAbierta.Application;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Policies;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static BodegaAbierta.UnitTests.Rules.TestData;

namespace BodegaAbierta.UnitTests.Rules;

public sealed class PolicyEngineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The engine as the app builds it: every strategy registered in AddApplication.</summary>
    private static PolicyEngine CreateEngine()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IBodegaDbContext>())
            .AddSingleton(Substitute.For<ITenantContext>())
            .AddSingleton(Substitute.For<ICurrentUser>())
            .AddSingleton(TimeProvider.System)
            .AddApplication();

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<PolicyEngine>();
    }

    private static PolicySet ActivePolicy(params RuleDefinition[] rules)
    {
        var policy = new PolicySet(FoodBankId, 1, null, "ana", ReceivedAt);
        policy.Edit(null, rules, ReceivedAt);
        policy.Submit("ana", ReceivedAt);
        policy.Approve("coordinadora", ReceivedAt);
        return policy;
    }

    [Fact]
    public void Every_rule_type_has_exactly_one_strategy()
        => CreateEngine().SupportedRuleTypes.Should().BeEquivalentTo(Enum.GetValues<RuleType>());

    [Fact]
    public async Task Without_an_active_policy_nothing_is_violated()
        => (await CreateEngine().EvaluateAsync(null, Distribution(Slot()), Ct)).Should().BeEmpty();

    [Fact]
    public async Task Only_rules_that_apply_to_the_kind_are_evaluated()
    {
        var policy = ActivePolicy(
            new RuleDefinition(RuleType.DonorDropOffHours, """{"days":["Monday"],"open":"08:00","close":"09:00"}"""),
            new RuleDefinition(RuleType.PickupWindow, """{"days":["Monday"],"open":"08:00","close":"09:00"}"""));

        // A Tuesday pickup slot: PickupWindow applies and fails; DonorDropOffHours does not apply at all.
        var violations = await CreateEngine().EvaluateAsync(
            policy, EvaluationContext.For(new PickupSlotProposal(TuesdayTen, TuesdayTen.AddHours(1), 5)), Ct);

        violations.Should().ContainSingle().Which.RuleType.Should().Be(RuleType.PickupWindow);
    }

    [Fact]
    public void Parameter_validation_is_dispatched_to_the_rule_strategy()
    {
        var engine = CreateEngine();

        engine.ValidateParameters(RuleType.BeneficiaryLimit, """{"maxKg":0,"periodDays":7}""")
            .Should().ContainSingle(e => e.Contains("maxKg", StringComparison.Ordinal));
        engine.ValidateParameters(RuleType.FefoAllocation, "{}").Should().BeEmpty();
    }

    [Fact]
    public void Evaluation_is_allowed_with_warnings_but_not_with_errors()
    {
        var warning = new RuleViolation(RuleType.ExpiryAlert, "expiry.soon", ViolationSeverity.Warning, "w");
        var error = new RuleViolation(null, "slot.full", ViolationSeverity.Error, "e");

        new PolicyEvaluation(null, [warning]).Allowed.Should().BeTrue();
        new PolicyEvaluation(null, [warning, error]).Allowed.Should().BeFalse();

        var act = () => new PolicyEvaluation(null, [error]).EnsureAllowed();
        act.Should().Throw<PolicyViolationException>().Which.Violations.Should().ContainSingle();
    }
}
