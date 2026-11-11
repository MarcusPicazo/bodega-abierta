using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Distributions;
using BodegaAbierta.Application.Rules;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Policies;

/// <summary>
/// A proposed operation to check without saving anything. Which fields are required depends on Kind:
/// Distribution (beneficiaryId, pickupSlotId, lines), PickupSlot (startsAt, endsAt, capacity),
/// DonorDropOff (donorId, at).
/// </summary>
public sealed record EvaluatePolicyRequest(
    EvaluationKind Kind,
    Guid? BeneficiaryId = null,
    Guid? PickupSlotId = null,
    IReadOnlyList<DistributionLineRequest>? Lines = null,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null,
    int? Capacity = null,
    Guid? DonorId = null,
    DateTimeOffset? At = null);

public sealed record PlannedAllocationResponse(int LineIndex, Guid ProductId, Guid InventoryLotId, decimal Quantity, DateOnly? ExpiresOn);

public sealed record PolicyEvaluationResponse(
    bool Allowed,
    Guid? PolicySetId,
    int? PolicySetVersion,
    IReadOnlyList<RuleViolation> Violations,
    IReadOnlyList<PlannedAllocationResponse> Allocation);

internal sealed class EvaluatePolicyRequestValidator : AbstractValidator<EvaluatePolicyRequest>
{
    public EvaluatePolicyRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();

        When(x => x.Kind == EvaluationKind.Distribution, () =>
        {
            RuleFor(x => x.BeneficiaryId).NotEmpty();
            RuleFor(x => x.PickupSlotId).NotEmpty();
            RuleFor(x => x.Lines!).NotNull().SetValidator(new DistributionLinesValidator()).OverridePropertyName("lines");
        });

        When(x => x.Kind == EvaluationKind.PickupSlot, () =>
        {
            RuleFor(x => x.StartsAt).NotNull();
            RuleFor(x => x.EndsAt).NotNull().GreaterThan(x => x.StartsAt).When(x => x.StartsAt is not null);
            RuleFor(x => x.Capacity).NotNull().InclusiveBetween(1, Domain.Distributions.PickupSlot.MaxCapacity);
        });

        When(x => x.Kind == EvaluationKind.DonorDropOff, () =>
        {
            RuleFor(x => x.DonorId).NotEmpty();
            RuleFor(x => x.At).NotNull();
        });
    }
}

public sealed class PolicyEvaluationService(
    IBodegaDbContext db,
    ITenantContext tenant,
    TimeProvider timeProvider,
    DistributionPlanner planner,
    PolicyGate policyGate,
    IValidator<EvaluatePolicyRequest> validator)
{
    public async Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenant.RequireFoodBankId();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        IReadOnlyList<RuleViolation> feasibility = [];
        IReadOnlyList<PlannedAllocationResponse> allocation = [];
        EvaluationContext context;

        switch (request.Kind)
        {
            case EvaluationKind.Distribution:
                var (proposal, checks) = await planner.PlanAsync(
                    request.BeneficiaryId!.Value, request.PickupSlotId!.Value, request.Lines!, timeProvider.GetUtcNow(), track: false, cancellationToken);
                feasibility = checks;
                allocation = proposal.Lines
                    .SelectMany(l => l.Plan.Picks.Select(p => new PlannedAllocationResponse(l.Index, l.Product.Id, p.Lot.Id, p.Quantity, p.Lot.ExpiresOn)))
                    .ToList();
                context = EvaluationContext.For(proposal);
                break;

            case EvaluationKind.PickupSlot:
                context = EvaluationContext.For(new PickupSlotProposal(request.StartsAt!.Value, request.EndsAt!.Value, request.Capacity!.Value));
                break;

            default:
                var donor = await db.Donors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DonorId, cancellationToken)
                    ?? throw new ValidationException([new ValidationFailure("donorId", $"Donor '{request.DonorId}' was not found.")]);
                if (!donor.IsActive)
                {
                    feasibility = [new RuleViolation(null, "donor.inactive", ViolationSeverity.Error, $"Donor '{donor.Name}' is inactive.")];
                }

                context = EvaluationContext.For(new DonorDropOffProposal(donor.Id, request.At!.Value));
                break;
        }

        var evaluation = await policyGate.EvaluateAsync(context, feasibility, cancellationToken);
        return new PolicyEvaluationResponse(
            evaluation.Allowed, evaluation.Policy?.Id, evaluation.Policy?.Version, evaluation.Violations, allocation);
    }
}
