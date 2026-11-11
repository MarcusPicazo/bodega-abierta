using BodegaAbierta.Domain.Policies;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Application.Rules.Evaluators;

/// <summary>Example: <c>{"maxKg":25,"periodDays":7}</c> = at most 25 kg per beneficiary in any 7-day window.</summary>
public sealed record BeneficiaryLimitParameters
{
    public required decimal MaxKg { get; init; }

    public required int PeriodDays { get; init; }
}

/// <summary>
/// Fair sharing: kilograms already scheduled for the beneficiary in the rolling period before the pickup,
/// plus what is requested now, must not exceed the maximum. Only products measured in kilograms count
/// (products counted in units have no weight in the catalog yet).
/// </summary>
public sealed class BeneficiaryLimitEvaluator(IDistributionHistory history) : PolicyRuleEvaluator<BeneficiaryLimitParameters>
{
    public override RuleType Type => RuleType.BeneficiaryLimit;

    public override bool AppliesTo(EvaluationKind kind) => kind == EvaluationKind.Distribution;

    protected override IEnumerable<string> Validate(BeneficiaryLimitParameters parameters)
    {
        if (parameters.MaxKg <= 0)
        {
            yield return "'maxKg' must be greater than zero.";
        }

        if (parameters.PeriodDays is < 1 or > 366)
        {
            yield return "'periodDays' must be between 1 and 366.";
        }
    }

    protected override async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        BeneficiaryLimitParameters parameters, EvaluationContext context, CancellationToken cancellationToken)
    {
        var proposal = context.Distribution!;
        var requestedKg = proposal.Lines
            .Where(l => l.Product.Unit == UnitOfMeasure.Kilogram)
            .Sum(l => l.RequestedQuantity);

        if (requestedKg == 0)
        {
            return [];
        }

        var to = proposal.Slot.StartsAt;
        var from = to.AddDays(-parameters.PeriodDays);
        var deliveredKg = await history.GetKilogramsScheduledAsync(proposal.Beneficiary.Id, from, to, cancellationToken);

        return deliveredKg + requestedKg <= parameters.MaxKg
            ? []
            :
            [
                Error(
                    "beneficiary.limit_exceeded",
                    $"'{proposal.Beneficiary.Name}' would receive {deliveredKg + requestedKg} kg in {parameters.PeriodDays} day(s) " +
                    $"({deliveredKg} kg already scheduled + {requestedKg} kg now); the limit is {parameters.MaxKg} kg."),
            ];
    }
}
