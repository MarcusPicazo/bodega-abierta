using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.Application.Rules.Evaluators;

/// <summary>Example: <c>{"days":["Tuesday","Thursday"],"open":"10:00","close":"13:00","maxSlotCapacity":40}</c>.</summary>
public sealed record PickupWindowParameters : WeeklyWindowParameters
{
    /// <summary>Upper bound for any slot's capacity (optional).</summary>
    public int? MaxSlotCapacity { get; init; }
}

/// <summary>Pickups only on the allowed days and hours, and no slot bigger than the configured maximum.</summary>
public sealed class PickupWindowEvaluator : PolicyRuleEvaluator<PickupWindowParameters>
{
    public override RuleType Type => RuleType.PickupWindow;

    public override bool AppliesTo(EvaluationKind kind) => kind is EvaluationKind.Distribution or EvaluationKind.PickupSlot;

    protected override IEnumerable<string> Validate(PickupWindowParameters parameters)
    {
        foreach (var error in WeeklyWindow.Validate(parameters))
        {
            yield return error;
        }

        if (parameters.MaxSlotCapacity is < 1 or > PickupSlot.MaxCapacity)
        {
            yield return $"'maxSlotCapacity' must be between 1 and {PickupSlot.MaxCapacity}.";
        }
    }

    protected override Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        PickupWindowParameters parameters, EvaluationContext context, CancellationToken cancellationToken)
    {
        var (startsAt, endsAt, capacity) = context.Kind == EvaluationKind.PickupSlot
            ? (context.PickupSlot!.StartsAt, context.PickupSlot.EndsAt, context.PickupSlot.Capacity)
            : (context.Distribution!.Slot.StartsAt, context.Distribution.Slot.EndsAt, context.Distribution.Slot.Capacity);

        var violations = new List<RuleViolation>();
        if (!WeeklyWindow.Contains(parameters, startsAt, endsAt))
        {
            violations.Add(Error(
                "pickup.outside_window",
                $"Pickups are only allowed {WeeklyWindow.Describe(parameters)}."));
        }

        if (parameters.MaxSlotCapacity is { } max && capacity > max)
        {
            violations.Add(Error(
                "pickup.capacity_exceeds_max",
                $"A pickup slot can hold at most {max} distributions; this one has capacity {capacity}."));
        }

        return Result(violations);
    }
}
