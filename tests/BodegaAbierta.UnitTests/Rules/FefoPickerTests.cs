using BodegaAbierta.Domain.Inventory;
using static BodegaAbierta.UnitTests.Rules.TestData;

namespace BodegaAbierta.UnitTests.Rules;

public sealed class FefoPickerTests
{
    private static readonly DateOnly PickupDate = Nov(10);

    [Fact]
    public void Picks_the_lot_that_expires_first()
    {
        var milk = Product("Leche", perishable: true);
        var later = Lot(milk, 10, Nov(20));
        var sooner = Lot(milk, 10, Nov(12));

        var plan = FefoPicker.Pick([later, sooner], 4, PickupDate);

        plan.Picks.Should().ContainSingle().Which.Should().Be(new LotPick(sooner, 4));
        plan.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Spills_over_to_the_next_lot_in_fefo_order()
    {
        var milk = Product("Leche", perishable: true);
        var first = Lot(milk, 3, Nov(12));
        var second = Lot(milk, 10, Nov(15));
        var third = Lot(milk, 10, Nov(20));

        var plan = FefoPicker.Pick([third, second, first], 8, PickupDate);

        plan.Picks.Should().Equal(new LotPick(first, 3), new LotPick(second, 5));
    }

    [Fact]
    public void Lots_without_expiry_go_last()
    {
        var beans = Product();
        var noExpiry = Lot(beans, 10, null);
        var expiring = Lot(beans, 10, Nov(30));

        FefoPicker.Pick([noExpiry, expiring], 5, PickupDate).Picks.Single().Lot.Should().BeSameAs(expiring);
    }

    [Fact]
    public void Same_expiry_is_broken_by_receipt_time()
    {
        var beans = Product();
        var newer = Lot(beans, 10, Nov(30), ReceivedAt.AddDays(2));
        var older = Lot(beans, 10, Nov(30), ReceivedAt);

        FefoPicker.Pick([newer, older], 5, PickupDate).Picks.Single().Lot.Should().BeSameAs(older);
    }

    [Fact]
    public void Lots_expired_by_the_pickup_date_are_skipped()
    {
        var milk = Product("Leche", perishable: true);
        var expiresBeforePickup = Lot(milk, 10, Nov(9));
        var expiresOnPickupDay = Lot(milk, 10, Nov(10));

        FefoPicker.Pick([expiresBeforePickup, expiresOnPickupDay], 5, PickupDate)
            .Picks.Single().Lot.Should().BeSameAs(expiresOnPickupDay);
    }

    [Fact]
    public void Reports_the_shortfall_when_stock_is_not_enough()
    {
        var beans = Product();

        var plan = FefoPicker.Pick([Lot(beans, 2, null), Lot(beans, 3, null)], 8, PickupDate);

        plan.Shortfall.Should().Be(3);
        plan.IsComplete.Should().BeFalse();
        plan.Picks.Sum(p => p.Quantity).Should().Be(5);
    }
}
