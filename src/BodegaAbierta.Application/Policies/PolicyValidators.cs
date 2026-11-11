using System.Text.Json;
using BodegaAbierta.Domain.Policies;
using FluentValidation;

namespace BodegaAbierta.Application.Policies;

/// <summary>Shape of one rule. Per-type parameter schemas are validated in Phase 4 (rules engine).</summary>
internal sealed class PolicyRuleRequestValidator : AbstractValidator<PolicyRuleRequest>
{
    public PolicyRuleRequestValidator()
    {
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.Parameters)
            .Must(p => p.ValueKind == JsonValueKind.Object)
            .WithMessage("'Parameters' must be a JSON object.");
    }
}

internal static class PolicyRuleListRules
{
    public const int MaxRules = 20;

    public static bool WithinLimit(IReadOnlyList<PolicyRuleRequest>? rules) => rules is null || rules.Count <= MaxRules;

    public static bool TypesAreUnique(IReadOnlyList<PolicyRuleRequest>? rules)
        => rules is null || rules.Select(r => r.Type).Distinct().Count() == rules.Count;
}

internal sealed class CreatePolicySetRequestValidator : AbstractValidator<CreatePolicySetRequest>
{
    public CreatePolicySetRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(PolicySet.DescriptionMaxLength);

        RuleFor(x => x.Rules).NotNull().When(x => x.CopyFromPolicySetId is null)
            .WithMessage("'Rules' is required unless 'CopyFromPolicySetId' is given.");
        RuleFor(x => x.Rules).Null().When(x => x.CopyFromPolicySetId is not null)
            .WithMessage("Send either 'Rules' or 'CopyFromPolicySetId', not both.");
        RuleFor(x => x.Rules)
            .Must(PolicyRuleListRules.WithinLimit).WithMessage($"A policy set can have at most {PolicyRuleListRules.MaxRules} rules.")
            .Must(PolicyRuleListRules.TypesAreUnique).WithMessage("Each rule type can appear only once.");
        RuleForEach(x => x.Rules).SetValidator(new PolicyRuleRequestValidator());
    }
}

internal sealed class UpdatePolicySetRequestValidator : AbstractValidator<UpdatePolicySetRequest>
{
    public UpdatePolicySetRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(PolicySet.DescriptionMaxLength);
        RuleFor(x => x.Rules)
            .NotNull()
            .Must(PolicyRuleListRules.WithinLimit).WithMessage($"A policy set can have at most {PolicyRuleListRules.MaxRules} rules.")
            .Must(PolicyRuleListRules.TypesAreUnique).WithMessage("Each rule type can appear only once.");
        RuleForEach(x => x.Rules).SetValidator(new PolicyRuleRequestValidator());
    }
}

internal sealed class RejectPolicySetRequestValidator : AbstractValidator<RejectPolicySetRequest>
{
    public RejectPolicySetRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(PolicySet.ReasonMaxLength);
    }
}
