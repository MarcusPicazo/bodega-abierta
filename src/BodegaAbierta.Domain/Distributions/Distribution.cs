using BodegaAbierta.Domain.Beneficiaries;
using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Domain.Distributions;

public enum DistributionStatus
{
    Scheduled = 0,
    PickedUp = 1,
    Cancelled = 2,
}

/// <summary>
/// Food handed to a beneficiary in a pickup slot. Aggregate root: allocations are added only through
/// <see cref="Allocate"/>, which also takes the quantity out of the lot.
/// </summary>
public sealed class Distribution : VersionedEntity, ITenantOwned
{
    public const int ActorMaxLength = 200;

    private readonly List<DistributionAllocation> _allocations = [];

    private Distribution() { } // EF Core

    /// <param name="policySetId">The Active policy version that allowed it (null if none was active).</param>
    public Distribution(Beneficiary beneficiary, PickupSlot slot, Guid? policySetId, string createdBy, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(beneficiary);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (beneficiary.FoodBankId != slot.FoodBankId)
        {
            throw new ArgumentException("Beneficiary and pickup slot must belong to the same food bank.", nameof(slot));
        }

        if (!beneficiary.IsActive)
        {
            throw new DomainRuleException("beneficiary.inactive", $"Beneficiary '{beneficiary.Name}' is inactive.");
        }

        slot.Reserve();

        FoodBankId = beneficiary.FoodBankId;
        BeneficiaryId = beneficiary.Id;
        PickupSlotId = slot.Id;
        ScheduledFor = slot.StartsAt;
        PolicySetId = policySetId;
        Status = DistributionStatus.Scheduled;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid FoodBankId { get; private set; }

    public Guid BeneficiaryId { get; private set; }

    public Guid PickupSlotId { get; private set; }

    /// <summary>Copied from the slot: the instant used for "kg per period" limits.</summary>
    public DateTimeOffset ScheduledFor { get; private set; }

    public Guid? PolicySetId { get; private set; }

    public DistributionStatus Status { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<DistributionAllocation> Allocations => _allocations.AsReadOnly();

    public DistributionAllocation Allocate(InventoryLot lot, decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(lot);
        if (lot.FoodBankId != FoodBankId)
        {
            throw new ArgumentException("The lot belongs to another food bank.", nameof(lot));
        }

        lot.Allocate(quantity);
        var allocation = new DistributionAllocation(FoodBankId, Id, lot, quantity);
        _allocations.Add(allocation);
        return allocation;
    }

    // TODO(Marcus) [Phase 4]: add `public void Cancel(PickupSlot slot, IReadOnlyCollection<InventoryLot> lots)`.
    // Only Scheduled distributions can be cancelled (else DomainRuleException "distribution.not_cancellable").
    // Give every allocation's quantity back to its lot (InventoryLot.Release, your other TODO), release the
    // slot (PickupSlot.Release), set Status = Cancelled. Check that `slot.Id == PickupSlotId` and that every
    // allocation's lot is in `lots`. Fill in the skipped stubs in tests/BodegaAbierta.UnitTests/Distributions/DistributionTests.cs.
}

public sealed class DistributionAllocation : Entity, ITenantOwned
{
    private DistributionAllocation() { } // EF Core

    internal DistributionAllocation(Guid foodBankId, Guid distributionId, InventoryLot lot, decimal quantity)
    {
        FoodBankId = foodBankId;
        DistributionId = distributionId;
        InventoryLotId = lot.Id;
        ProductId = lot.ProductId;
        Unit = lot.Unit;
        Quantity = quantity;
    }

    public Guid FoodBankId { get; private set; }

    public Guid DistributionId { get; private set; }

    public Guid InventoryLotId { get; private set; }

    public Guid ProductId { get; private set; }

    public UnitOfMeasure Unit { get; private set; }

    public decimal Quantity { get; private set; }
}
