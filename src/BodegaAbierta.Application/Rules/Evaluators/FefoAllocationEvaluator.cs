using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.Application.Rules.Evaluators;

/// <summary>No parameters: <c>{}</c>.</summary>
public sealed record FefoAllocationParameters;

/// <summary>
/// "Always allocate the lot that expires first." Automatic plans are FEFO by construction, so this rule
/// only has work to do when a person picked a lot by hand: it fails if a usable lot of the same product
/// expires earlier.
/// </summary>
public sealed class FefoAllocationEvaluator : PolicyRuleEvaluator<FefoAllocationParameters>
{
    public override RuleType Type => RuleType.FefoAllocation;

    public override bool AppliesTo(EvaluationKind kind) => kind == EvaluationKind.Distribution;

    protected override Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        FefoAllocationParameters parameters, EvaluationContext context, CancellationToken cancellationToken)
    {
        var proposal = context.Distribution!;
        var violations = new List<RuleViolation>();

        foreach (var line in proposal.Lines.Where(l => l.ManualLotId is not null && l.Plan.Picks.Count == 1))
        {
            var chosen = line.Plan.Picks[0].Lot;
            var earlier = FefoPicker.Order(line.Candidates
                    .Where(l => l.Id != chosen.Id && l.IsAllocatableOn(proposal.Slot.PickupDate)))
                .FirstOrDefault();

            if (earlier is not null && (earlier.ExpiresOn ?? DateOnly.MaxValue) < (chosen.ExpiresOn ?? DateOnly.MaxValue))
            {
                violations.Add(Error(
                    "fefo.not_first_expiring",
                    $"Lot {chosen.Id} of '{line.Product.Name}' expires {Format(chosen.ExpiresOn)}, but lot {earlier.Id} " +
                    $"expires earlier ({Format(earlier.ExpiresOn)}) and must be used first.",
                    line.Index));
            }
        }

        return Result(violations);
    }

    private static string Format(DateOnly? date) => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "never";
}
