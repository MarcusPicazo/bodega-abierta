using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.Application.Rules.Evaluators;

/// <summary>When donors may deliver. Parameters: a <see cref="WeeklyWindowParameters"/>.</summary>
public sealed class DonorDropOffHoursEvaluator : PolicyRuleEvaluator<WeeklyWindowParameters>
{
    public override RuleType Type => RuleType.DonorDropOffHours;

    public override bool AppliesTo(EvaluationKind kind) => kind == EvaluationKind.DonorDropOff;

    protected override IEnumerable<string> Validate(WeeklyWindowParameters parameters) => WeeklyWindow.Validate(parameters);

    protected override Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        WeeklyWindowParameters parameters, EvaluationContext context, CancellationToken cancellationToken)
    {
        var at = context.DonorDropOff!.At;
        return Result(WeeklyWindow.Contains(parameters, at, at)
            ? []
            : [Error("donor.outside_dropoff_hours", $"Donations are received {WeeklyWindow.Describe(parameters)}.")]);
    }
}
