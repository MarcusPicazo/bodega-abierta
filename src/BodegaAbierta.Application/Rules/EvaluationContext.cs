using BodegaAbierta.Domain.Beneficiaries;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Application.Rules;

/// <summary>What is being evaluated. Each rule declares which kinds it applies to.</summary>
public enum EvaluationKind
{
    Distribution = 0,
    PickupSlot = 1,
    DonorDropOff = 2,
}

public enum ViolationSeverity
{
    /// <summary>Blocks the operation (allowed = false).</summary>
    Error = 0,

    /// <summary>Informational; the operation is still allowed.</summary>
    Warning = 1,
}

/// <param name="RuleType">The policy rule that produced it, or null for built-in feasibility checks (stock, capacity...).</param>
/// <param name="LineIndex">Zero-based request line it refers to, when it refers to one.</param>
public sealed record RuleViolation(RuleType? RuleType, string Code, ViolationSeverity Severity, string Message, int? LineIndex = null);

/// <summary>One requested product line with its allocation plan (FEFO, or the lot the user picked).</summary>
public sealed record PlannedLine(
    int Index,
    Product Product,
    decimal RequestedQuantity,
    Guid? ManualLotId,
    IReadOnlyList<InventoryLot> Candidates,
    FefoPlan Plan);

public sealed record DistributionProposal(Beneficiary Beneficiary, PickupSlot Slot, IReadOnlyList<PlannedLine> Lines);

public sealed record PickupSlotProposal(DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity);

public sealed record DonorDropOffProposal(Guid DonorId, DateTimeOffset At);

/// <summary>Everything a rule may look at. Exactly the member matching <see cref="Kind"/> is set.</summary>
public sealed record EvaluationContext(
    EvaluationKind Kind,
    DistributionProposal? Distribution = null,
    PickupSlotProposal? PickupSlot = null,
    DonorDropOffProposal? DonorDropOff = null)
{
    public static EvaluationContext For(DistributionProposal proposal) => new(EvaluationKind.Distribution, Distribution: proposal);

    public static EvaluationContext For(PickupSlotProposal proposal) => new(EvaluationKind.PickupSlot, PickupSlot: proposal);

    public static EvaluationContext For(DonorDropOffProposal proposal) => new(EvaluationKind.DonorDropOff, DonorDropOff: proposal);
}
