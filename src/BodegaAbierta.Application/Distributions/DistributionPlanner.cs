using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Inventory;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Distributions;

/// <param name="LotId">Optional: pick this lot by hand instead of letting FEFO choose.</param>
public sealed record DistributionLineRequest(Guid ProductId, decimal Quantity, Guid? LotId);

/// <summary>
/// Turns a request (beneficiary, slot, product lines) into a <see cref="DistributionProposal"/> with an
/// allocation plan, plus the built-in feasibility checks that apply with or without a policy
/// (stock, slot capacity, inactive beneficiary...). Unknown ids are input errors (400).
/// </summary>
public sealed class DistributionPlanner(IBodegaDbContext db)
{
    public async Task<(DistributionProposal Proposal, IReadOnlyList<RuleViolation> Feasibility)> PlanAsync(
        Guid beneficiaryId,
        Guid pickupSlotId,
        IReadOnlyList<DistributionLineRequest> lines,
        DateTimeOffset now,
        bool track,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        // Tracked when the plan will be executed (lots and slot get updated), untracked for a dry run.
        var beneficiaries = track ? db.Beneficiaries : db.Beneficiaries.AsNoTracking();
        var slots = track ? db.PickupSlots : db.PickupSlots.AsNoTracking();
        var products = track ? db.Products : db.Products.AsNoTracking();
        var lots = track ? db.InventoryLots : db.InventoryLots.AsNoTracking();

        var beneficiary = await beneficiaries.FirstOrDefaultAsync(b => b.Id == beneficiaryId, cancellationToken);
        var slot = await slots.FirstOrDefaultAsync(s => s.Id == pickupSlotId, cancellationToken);

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var productsById = await products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var manualLotIds = lines.Where(l => l.LotId is not null).Select(l => l.LotId!.Value).ToList();
        var candidateLots = await lots
            .Where(l => productIds.Contains(l.ProductId) || manualLotIds.Contains(l.Id))
            .Where(l => manualLotIds.Contains(l.Id) || (l.Status == LotStatus.Available && l.QuantityOnHand > 0))
            .ToListAsync(cancellationToken);

        var failures = new List<ValidationFailure>();
        if (beneficiary is null)
        {
            failures.Add(new ValidationFailure("beneficiaryId", $"Beneficiary '{beneficiaryId}' was not found."));
        }

        if (slot is null)
        {
            failures.Add(new ValidationFailure("pickupSlotId", $"Pickup slot '{pickupSlotId}' was not found."));
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (!productsById.ContainsKey(lines[i].ProductId))
            {
                failures.Add(new ValidationFailure($"lines[{i}].productId", $"Product '{lines[i].ProductId}' was not found."));
            }

            if (lines[i].LotId is { } lotId && candidateLots.All(l => l.Id != lotId))
            {
                failures.Add(new ValidationFailure($"lines[{i}].lotId", $"Inventory lot '{lotId}' was not found."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var feasibility = new List<RuleViolation>();
        if (!beneficiary!.IsActive)
        {
            feasibility.Add(Feasibility("beneficiary.inactive", $"Beneficiary '{beneficiary.Name}' is inactive."));
        }

        if (slot!.StartsAt < now)
        {
            feasibility.Add(Feasibility("slot.in_past", "The pickup slot has already started."));
        }

        if (slot.IsFull)
        {
            feasibility.Add(Feasibility("slot.full", $"The pickup slot is full ({slot.ReservedCount}/{slot.Capacity})."));
        }

        var planned = new List<PlannedLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var product = productsById[line.ProductId];
            var candidates = candidateLots.Where(l => l.ProductId == product.Id).ToList();
            FefoPlan plan;
            var lotRejected = false;

            if (line.LotId is { } lotId)
            {
                var lot = candidateLots.Single(l => l.Id == lotId);
                if (lot.ProductId != product.Id)
                {
                    feasibility.Add(Feasibility("lot.product_mismatch", $"Lot {lot.Id} is not '{product.Name}'.", i));
                    (plan, lotRejected) = (new FefoPlan([], line.Quantity), true);
                }
                else if (!lot.IsAllocatableOn(slot.PickupDate))
                {
                    feasibility.Add(Feasibility("lot.not_allocatable", $"Lot {lot.Id} is not available or expires before the pickup.", i));
                    (plan, lotRejected) = (new FefoPlan([], line.Quantity), true);
                }
                else
                {
                    var take = Math.Min(lot.QuantityOnHand, line.Quantity);
                    plan = new FefoPlan([new LotPick(lot, take)], line.Quantity - take);
                }
            }
            else
            {
                plan = FefoPicker.Pick(candidates, line.Quantity, slot.PickupDate);
            }

            if (!plan.IsComplete && !lotRejected)
            {
                feasibility.Add(Feasibility(
                    "inventory.insufficient",
                    $"Not enough '{product.Name}' for the pickup date: {line.Quantity - plan.Shortfall} of {line.Quantity} available.",
                    i));
            }

            planned.Add(new PlannedLine(i, product, line.Quantity, line.LotId, candidates, plan));
        }

        return (new DistributionProposal(beneficiary, slot, planned), feasibility);
    }

    private static RuleViolation Feasibility(string code, string message, int? lineIndex = null)
        => new(null, code, ViolationSeverity.Error, message, lineIndex);
}
