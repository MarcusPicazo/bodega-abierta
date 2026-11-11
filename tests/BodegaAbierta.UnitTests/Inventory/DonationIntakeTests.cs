using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.UnitTests.Inventory;

public sealed class DonationIntakeTests
{
    private static readonly Guid FoodBankId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 10, 9, 30, 0, TimeSpan.FromHours(-6));

    private static Donor ActiveDonor() => new(FoodBankId, "Supermercado La Esperanza", DonorType.Business, null, null);

    private static Product Perishable() => new(FoodBankId, "Leche entera 1 L", ProductCategory.Dairy, UnitOfMeasure.Unit, isPerishable: true);

    private static Product Dry() => new(FoodBankId, "Frijol negro", ProductCategory.Grains, UnitOfMeasure.Kilogram, isPerishable: false);

    private static DonationIntake NewIntake() => new(ActiveDonor(), "Ana (almacén)", ReceivedAt, notes: null);

    private static void ShouldViolate(Action act, string code)
        => act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(code);

    [Fact]
    public void AddLot_creates_an_available_lot_with_the_full_quantity_on_hand()
    {
        var intake = NewIntake();
        var milk = Perishable();

        var lot = intake.AddLot(milk, LocationId, 24m, new DateOnly(2026, 11, 20));

        lot.FoodBankId.Should().Be(FoodBankId);
        lot.DonationIntakeId.Should().Be(intake.Id);
        lot.ProductId.Should().Be(milk.Id);
        lot.Unit.Should().Be(UnitOfMeasure.Unit);
        lot.InitialQuantity.Should().Be(24m);
        lot.QuantityOnHand.Should().Be(24m);
        lot.Status.Should().Be(LotStatus.Available);
        lot.ReceivedAt.Should().Be(ReceivedAt);
        intake.Lots.Should().ContainSingle().Which.Should().BeSameAs(lot);
    }

    [Fact]
    public void Non_perishable_product_does_not_need_an_expiry_date()
    {
        var lot = NewIntake().AddLot(Dry(), LocationId, 50m, expiresOn: null);

        lot.ExpiresOn.Should().BeNull();
    }

    [Fact]
    public void Perishable_product_requires_an_expiry_date()
        => ShouldViolate(() => NewIntake().AddLot(Perishable(), LocationId, 10m, expiresOn: null), "lot.expiry_required");

    [Fact]
    public void Food_that_already_expired_cannot_be_received()
        => ShouldViolate(() => NewIntake().AddLot(Perishable(), LocationId, 10m, new DateOnly(2026, 11, 9)), "lot.already_expired");

    [Fact]
    public void Food_expiring_on_the_receipt_day_can_still_be_received()
    {
        var lot = NewIntake().AddLot(Perishable(), LocationId, 10m, new DateOnly(2026, 11, 10));

        lot.ExpiresOn.Should().Be(new DateOnly(2026, 11, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_must_be_positive(decimal quantity)
        => ShouldViolate(() => NewIntake().AddLot(Dry(), LocationId, quantity, null), "lot.quantity_not_positive");

    [Fact]
    public void Product_from_another_food_bank_is_rejected()
    {
        var foreignProduct = new Product(Guid.NewGuid(), "Arroz", ProductCategory.Grains, UnitOfMeasure.Kilogram, false);

        ShouldViolate(() => NewIntake().AddLot(foreignProduct, LocationId, 5m, null), "lot.product_other_food_bank");
    }

    [Fact]
    public void Inactive_donor_cannot_deliver()
    {
        var donor = ActiveDonor();
        donor.Update(donor.Name, donor.Type, null, null, isActive: false);

        ShouldViolate(() => _ = new DonationIntake(donor, "Ana", ReceivedAt, null), "donor.inactive");
    }
}
