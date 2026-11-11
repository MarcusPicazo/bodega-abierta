using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Products;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Distributions;

public sealed record CreateDistributionRequest(Guid BeneficiaryId, Guid PickupSlotId, IReadOnlyList<DistributionLineRequest> Lines);

public sealed record DistributionAllocationResponse(Guid InventoryLotId, Guid ProductId, UnitOfMeasure Unit, decimal Quantity);

/// <param name="Warnings">Non-blocking findings at creation time (e.g. ExpiryAlert); empty on GET.</param>
public sealed record DistributionResponse(
    Guid Id,
    Guid BeneficiaryId,
    Guid PickupSlotId,
    DateTimeOffset ScheduledFor,
    DistributionStatus Status,
    Guid? PolicySetId,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DistributionAllocationResponse> Allocations,
    IReadOnlyList<RuleViolation> Warnings);

internal sealed class DistributionLinesValidator : AbstractValidator<IReadOnlyList<DistributionLineRequest>>
{
    public const int MaxLines = 50;

    public DistributionLinesValidator()
    {
        RuleFor(lines => lines)
            .NotEmpty().WithMessage("At least one line is required.")
            .Must(lines => lines is null || lines.Count <= MaxLines).WithMessage($"At most {MaxLines} lines.")
            .Must(lines => lines is null || lines.Select(l => l.ProductId).Distinct().Count() == lines.Count)
            .WithMessage("Each product can appear in only one line.");

        RuleForEach(lines => lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).PrecisionScale(18, 3, ignoreTrailingZeros: true);
        });
    }
}

internal sealed class CreateDistributionRequestValidator : AbstractValidator<CreateDistributionRequest>
{
    public CreateDistributionRequestValidator()
    {
        RuleFor(x => x.BeneficiaryId).NotEmpty();
        RuleFor(x => x.PickupSlotId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().SetValidator(new DistributionLinesValidator()).OverridePropertyName("lines");
    }
}

public sealed class DistributionService(
    IBodegaDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    DistributionPlanner planner,
    PolicyGate policyGate,
    IValidator<CreateDistributionRequest> validator)
{
    /// <summary>
    /// Plan (FEFO) -&gt; evaluate against the Active policy -&gt; allocate lots and reserve the slot in one
    /// SaveChanges. Lots and slot are rowversion-protected: a concurrent request that touched them first
    /// makes this save fail instead of overselling stock or overbooking the slot.
    /// </summary>
    public async Task<DistributionResponse> CreateAsync(CreateDistributionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenant.RequireFoodBankId();
        var actor = currentUser.RequireUserId();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var (proposal, feasibility) = await planner.PlanAsync(
            request.BeneficiaryId, request.PickupSlotId, request.Lines, now, track: true, cancellationToken);

        var evaluation = await policyGate.EvaluateAsync(EvaluationContext.For(proposal), feasibility, cancellationToken);
        evaluation.EnsureAllowed();

        var distribution = new Distribution(proposal.Beneficiary, proposal.Slot, evaluation.Policy?.Id, actor, now);
        foreach (var pick in proposal.Lines.SelectMany(l => l.Plan.Picks))
        {
            distribution.Allocate(pick.Lot, pick.Quantity);
        }

        db.Distributions.Add(distribution);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Another distribution took some of this stock or slot capacity at the same time. Please retry.", ex);
        }

        return ToResponse(distribution, evaluation.Warnings);
    }

    public async Task<DistributionResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var distribution = await db.Distributions.AsNoTracking()
            .Include(d => d.Allocations)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Distribution", id);

        return ToResponse(distribution, []);
    }

    private static DistributionResponse ToResponse(Distribution d, IReadOnlyList<RuleViolation> warnings)
        => new(
            d.Id,
            d.BeneficiaryId,
            d.PickupSlotId,
            d.ScheduledFor,
            d.Status,
            d.PolicySetId,
            d.CreatedBy,
            d.CreatedAt,
            d.Allocations.Select(a => new DistributionAllocationResponse(a.InventoryLotId, a.ProductId, a.Unit, a.Quantity)).ToList(),
            warnings);
}
