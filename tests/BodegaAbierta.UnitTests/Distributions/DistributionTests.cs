using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Inventory;
using static BodegaAbierta.UnitTests.Rules.TestData;

namespace BodegaAbierta.UnitTests.Distributions;

public sealed class DistributionTests
{
    private static void ShouldViolate(Action act, string code)
        => act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(code);

    [Fact]
    public void Scheduling_reserves_a_spot_and_allocating_takes_stock_from_the_lot()
    {
        var beans = Product();
        var lot = Lot(beans, 10, null);
        var slot = Slot(capacity: 2);

        var distribution = new Distribution(Beneficiary(), slot, policySetId: null, "ana", ReceivedAt);
        distribution.Allocate(lot, 4);

        slot.ReservedCount.Should().Be(1);
        lot.QuantityOnHand.Should().Be(6);
        lot.Status.Should().Be(LotStatus.Available);
        distribution.ScheduledFor.Should().Be(slot.StartsAt);
        distribution.Allocations.Should().ContainSingle().Which.Quantity.Should().Be(4);
    }

    [Fact]
    public void Taking_the_whole_lot_marks_it_distributed()
    {
        var lot = Lot(Product(), 5, null);

        new Distribution(Beneficiary(), Slot(), null, "ana", ReceivedAt).Allocate(lot, 5);

        lot.QuantityOnHand.Should().Be(0);
        lot.Status.Should().Be(LotStatus.Distributed);
    }

    [Fact]
    public void Cannot_take_more_than_the_lot_has()
    {
        var lot = Lot(Product(), 5, null);
        var distribution = new Distribution(Beneficiary(), Slot(), null, "ana", ReceivedAt);

        ShouldViolate(() => distribution.Allocate(lot, 6), "lot.insufficient_quantity");
    }

    [Fact]
    public void A_full_slot_cannot_be_booked()
    {
        var slot = Slot(capacity: 1);
        _ = new Distribution(Beneficiary("Familia A"), slot, null, "ana", ReceivedAt);

        ShouldViolate(() => _ = new Distribution(Beneficiary("Familia B"), slot, null, "ana", ReceivedAt), "slot.full");
    }

    [Fact]
    public void Inactive_beneficiaries_cannot_receive()
    {
        var beneficiary = Beneficiary();
        beneficiary.SetActive(false);

        ShouldViolate(() => _ = new Distribution(beneficiary, Slot(), null, "ana", ReceivedAt), "beneficiary.inactive");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Slot_capacity_is_bounded(int capacity)
        => ShouldViolate(() => Slot(capacity: capacity), "slot.invalid_capacity");

    private const string CancelTodo = "TODO(Marcus): implement Distribution.Cancel (+ InventoryLot.Release, PickupSlot.Release)";

    [Fact(Skip = CancelTodo)]
    public void Cancel_returns_stock_to_the_lots_and_frees_the_slot()
    {
    }

    [Fact(Skip = CancelTodo)]
    public void Cancel_makes_a_distributed_lot_available_again()
    {
    }

    [Fact(Skip = CancelTodo)]
    public void Cancel_twice_violates_distribution_not_cancellable()
    {
    }
}
