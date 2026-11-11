using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Domain.Inventory;

public enum LotStatus
{
    Available = 0,
    Reserved = 1,
    Distributed = 2,
    Discarded = 3,
}

/// <summary>
/// A quantity of one product received together, with a single expiry date and location.
/// Created only through <see cref="DonationIntake.AddLot"/>.
/// </summary>
public sealed class InventoryLot : VersionedEntity, ITenantOwned
{
    private InventoryLot() { } // EF Core

    internal InventoryLot(
        Guid foodBankId,
        Guid donationIntakeId,
        Product product,
        Guid storageLocationId,
        decimal quantity,
        DateOnly? expiresOn,
        DateTimeOffset receivedAt)
    {
        FoodBankId = foodBankId;
        DonationIntakeId = donationIntakeId;
        ProductId = product.Id;
        Unit = product.Unit;
        StorageLocationId = storageLocationId;
        InitialQuantity = quantity;
        QuantityOnHand = quantity;
        ExpiresOn = expiresOn;
        ReceivedAt = receivedAt;
        Status = LotStatus.Available;
    }

    public Guid FoodBankId { get; private set; }

    public Guid DonationIntakeId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid StorageLocationId { get; private set; }

    /// <summary>Copied from the product at receipt time so history is stable if the catalog changes.</summary>
    public UnitOfMeasure Unit { get; private set; }

    public decimal InitialQuantity { get; private set; }

    public decimal QuantityOnHand { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public LotStatus Status { get; private set; }

    // TODO(Marcus): add `public void MoveTo(Guid storageLocationId)`. Rules: Guid.Empty -> ArgumentException;
    // only lots in status Available or Reserved can move (otherwise DomainRuleException with code
    // "lot.not_movable"); moving to the same location is a no-op. Fill in the skipped test stubs in
    // tests/BodegaAbierta.UnitTests/Inventory/InventoryLotTests.cs and remove their Skip. (Next step, later: a
    // POST /api/v1/inventory-lots/{id}/move endpoint that requires If-Match, like PUT /donors/{id}.)
}
