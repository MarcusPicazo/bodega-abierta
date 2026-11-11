using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Domain.Inventory;

/// <summary>
/// One delivery from a donor. Aggregate root: inventory lots are created only through
/// <see cref="AddLot"/>, so the receipt rules live in one place.
/// </summary>
public sealed class DonationIntake : Entity, ITenantOwned
{
    public const int ReceivedByMaxLength = 200;
    public const int NotesMaxLength = 1000;

    private readonly List<InventoryLot> _lots = [];

    private DonationIntake() { } // EF Core

    public DonationIntake(Donor donor, string receivedBy, DateTimeOffset receivedAt, string? notes)
    {
        ArgumentNullException.ThrowIfNull(donor);
        ArgumentException.ThrowIfNullOrWhiteSpace(receivedBy);
        donor.EnsureCanDonate();

        FoodBankId = donor.FoodBankId;
        DonorId = donor.Id;
        ReceivedBy = receivedBy.Trim();
        ReceivedAt = receivedAt;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid FoodBankId { get; private set; }

    public Guid DonorId { get; private set; }

    public string ReceivedBy { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<InventoryLot> Lots => _lots.AsReadOnly();

    public InventoryLot AddLot(Product product, Guid storageLocationId, decimal quantity, DateOnly? expiresOn)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.FoodBankId != FoodBankId)
        {
            throw new DomainRuleException("lot.product_other_food_bank", "The product belongs to another food bank.");
        }

        if (storageLocationId == Guid.Empty)
        {
            throw new DomainRuleException("lot.location_required", "Every lot needs a storage location.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException("lot.quantity_not_positive", "Lot quantity must be greater than zero.");
        }

        if (product.IsPerishable && expiresOn is null)
        {
            throw new DomainRuleException(
                "lot.expiry_required", $"'{product.Name}' is perishable, so an expiry date is required.");
        }

        var receivedOn = DateOnly.FromDateTime(ReceivedAt.DateTime); // calendar date in the intake's own offset
        if (expiresOn is { } expiry && expiry < receivedOn)
        {
            throw new DomainRuleException(
                "lot.already_expired", $"'{product.Name}' expired on {expiry:yyyy-MM-dd}; expired food cannot be received.");
        }

        var lot = new InventoryLot(FoodBankId, Id, product, storageLocationId, quantity, expiresOn, ReceivedAt);
        _lots.Add(lot);
        return lot;
    }
}
