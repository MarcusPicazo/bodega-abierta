using System.Globalization;
using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.Application.Rules.Evaluators;

/// <summary>Example: <c>{"daysBeforeExpiry":3}</c>.</summary>
public sealed record ExpiryAlertParameters
{
    public const int MaxDays = 365;

    public required int DaysBeforeExpiry { get; init; }
}

/// <summary>
/// Flags lots that expire within N days of the pickup date. A WARNING, never a blocker: giving away food
/// that is about to expire is exactly what a food bank wants; staff just need to know.
/// The same N drives GET /api/v1/inventory-lots/expiry-alerts.
/// </summary>
public sealed class ExpiryAlertEvaluator : PolicyRuleEvaluator<ExpiryAlertParameters>
{
    public override RuleType Type => RuleType.ExpiryAlert;

    public override bool AppliesTo(EvaluationKind kind) => kind == EvaluationKind.Distribution;

    protected override IEnumerable<string> Validate(ExpiryAlertParameters parameters)
    {
        if (parameters.DaysBeforeExpiry is < 1 or > ExpiryAlertParameters.MaxDays)
        {
            yield return $"'daysBeforeExpiry' must be between 1 and {ExpiryAlertParameters.MaxDays}.";
        }
    }

    protected override Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        ExpiryAlertParameters parameters, EvaluationContext context, CancellationToken cancellationToken)
    {
        var proposal = context.Distribution!;
        var limit = proposal.Slot.PickupDate.AddDays(parameters.DaysBeforeExpiry);

        var warnings = proposal.Lines
            .SelectMany(line => line.Plan.Picks.Select(pick => (line, pick)))
            .Where(x => x.pick.Lot.ExpiresOn is { } expires && expires <= limit)
            .Select(x => Warning(
                "expiry.soon",
                $"Lot {x.pick.Lot.Id} of '{x.line.Product.Name}' expires {x.pick.Lot.ExpiresOn!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}, " +
                $"within {parameters.DaysBeforeExpiry} day(s) of the pickup: tell the beneficiary to use it first.",
                x.line.Index));

        return Result(warnings);
    }
}
