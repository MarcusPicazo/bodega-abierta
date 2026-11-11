using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Distributions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Distributions;

public sealed record CreatePickupSlotRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity);

public sealed record ListPickupSlotsQuery(DateTimeOffset? From = null, DateTimeOffset? To = null);

public sealed record PickupSlotResponse(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, int ReservedCount, string Version);

internal sealed class CreatePickupSlotRequestValidator : AbstractValidator<CreatePickupSlotRequest>
{
    public CreatePickupSlotRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.StartsAt).Must(s => s > timeProvider.GetUtcNow()).WithMessage("'Starts At' must be in the future.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt);
        RuleFor(x => x.Capacity).InclusiveBetween(1, PickupSlot.MaxCapacity);
    }
}

public sealed class PickupSlotService(
    IBodegaDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    PolicyGate policyGate,
    IValidator<CreatePickupSlotRequest> validator)
{
    /// <summary>Creates a slot if the Active policy's PickupWindow allows it (days, hours, max capacity).</summary>
    public async Task<PickupSlotResponse> CreateAsync(CreatePickupSlotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var foodBankId = tenant.RequireFoodBankId();
        currentUser.RequireUserId();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var evaluation = await policyGate.EvaluateAsync(
            EvaluationContext.For(new PickupSlotProposal(request.StartsAt, request.EndsAt, request.Capacity)), [], cancellationToken);
        evaluation.EnsureAllowed();

        var slot = new PickupSlot(foodBankId, request.StartsAt, request.EndsAt, request.Capacity);
        db.PickupSlots.Add(slot);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(slot);
    }

    public async Task<PickupSlotResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var slot = await db.PickupSlots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pickup slot", id);
        return ToResponse(slot);
    }

    public async Task<IReadOnlyList<PickupSlotResponse>> ListAsync(ListPickupSlotsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        tenant.RequireFoodBankId();

        var slots = db.PickupSlots.AsNoTracking();
        if (query.From is { } from)
        {
            slots = slots.Where(s => s.StartsAt >= from);
        }

        if (query.To is { } to)
        {
            slots = slots.Where(s => s.StartsAt < to);
        }

        var list = await slots.OrderBy(s => s.StartsAt).Take(200).ToListAsync(cancellationToken);
        return list.Select(ToResponse).ToList();
    }

    private static PickupSlotResponse ToResponse(PickupSlot s)
        => new(s.Id, s.StartsAt, s.EndsAt, s.Capacity, s.ReservedCount, VersionToken.Encode(s.RowVersion));
}
