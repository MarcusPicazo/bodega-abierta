using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Beneficiaries;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.UnitTests.Rules;

/// <summary>Builds domain objects through their real constructors/aggregates (no reflection hacks).</summary>
internal static class TestData
{
    public static readonly Guid FoodBankId = Guid.NewGuid();

    public static readonly TimeSpan Mexico = TimeSpan.FromHours(-6);

    /// <summary>Lots are received on 2026-11-01.</summary>
    public static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 9, 0, 0, Mexico);

    /// <summary>Tuesday 2026-11-10, 10:00-12:00 Mexico City.</summary>
    public static readonly DateTimeOffset TuesdayTen = new(2026, 11, 10, 10, 0, 0, Mexico);

    public static DateOnly Nov(int day) => new(2026, 11, day);

    public static Product Product(string name = "Frijol negro", UnitOfMeasure unit = UnitOfMeasure.Kilogram, bool perishable = false)
        => new(FoodBankId, name, ProductCategory.Grains, unit, perishable);

    public static InventoryLot Lot(Product product, decimal quantity, DateOnly? expiresOn, DateTimeOffset? receivedAt = null)
    {
        var donor = new Donor(FoodBankId, "Supermercado La Esperanza", DonorType.Business, null, null);
        var intake = new DonationIntake(donor, "ana", receivedAt ?? ReceivedAt, null);
        return intake.AddLot(product, Guid.NewGuid(), quantity, expiresOn);
    }

    public static PickupSlot Slot(DateTimeOffset? startsAt = null, int capacity = 10, TimeSpan? length = null)
    {
        var start = startsAt ?? TuesdayTen;
        return new PickupSlot(FoodBankId, start, start + (length ?? TimeSpan.FromHours(2)), capacity);
    }

    public static Beneficiary Beneficiary(string name = "Familia López") => new(FoodBankId, name, BeneficiaryType.Household, null);

    /// <summary>A planned line using FEFO over <paramref name="candidates"/>.</summary>
    public static PlannedLine FefoLine(int index, Product product, decimal quantity, PickupSlot slot, params InventoryLot[] candidates)
        => new(index, product, quantity, null, candidates, FefoPicker.Pick(candidates, quantity, slot.PickupDate));

    /// <summary>A planned line where a person picked <paramref name="chosen"/> by hand.</summary>
    public static PlannedLine ManualLine(int index, Product product, decimal quantity, InventoryLot chosen, params InventoryLot[] candidates)
        => new(index, product, quantity, chosen.Id, candidates, new FefoPlan([new LotPick(chosen, quantity)], 0));

    public static EvaluationContext Distribution(PickupSlot slot, params PlannedLine[] lines)
        => EvaluationContext.For(new DistributionProposal(Beneficiary(), slot, lines));
}
