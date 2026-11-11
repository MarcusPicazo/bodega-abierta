using BodegaAbierta.Application.Rules;
using BodegaAbierta.Application.Rules.Evaluators;
using BodegaAbierta.Domain.Products;
using NSubstitute;
using static BodegaAbierta.UnitTests.Rules.TestData;

namespace BodegaAbierta.UnitTests.Rules;

/// <summary>One test class per strategy would also be fine; these are grouped by rule with nested classes.</summary>
public static class RuleEvaluatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class FefoAllocation
    {
        private readonly FefoAllocationEvaluator _sut = new();

        [Fact]
        public async Task Manual_pick_of_a_later_expiring_lot_is_an_error()
        {
            var milk = Product("Leche", UnitOfMeasure.Unit, perishable: true);
            var sooner = Lot(milk, 10, Nov(12));
            var later = Lot(milk, 10, Nov(20));
            var slot = Slot();

            var violations = await _sut.EvaluateAsync("{}", Distribution(slot, ManualLine(0, milk, 2, later, sooner, later)), Ct);

            violations.Should().ContainSingle().Which.Should().Match<RuleViolation>(v =>
                v.Code == "fefo.not_first_expiring" && v.Severity == ViolationSeverity.Error && v.LineIndex == 0);
        }

        [Fact]
        public async Task Manual_pick_of_the_first_expiring_lot_is_fine()
        {
            var milk = Product("Leche", UnitOfMeasure.Unit, perishable: true);
            var sooner = Lot(milk, 10, Nov(12));
            var later = Lot(milk, 10, Nov(20));

            var violations = await _sut.EvaluateAsync("{}", Distribution(Slot(), ManualLine(0, milk, 2, sooner, sooner, later)), Ct);

            violations.Should().BeEmpty();
        }

        [Fact]
        public async Task Automatic_fefo_plans_always_pass()
        {
            var milk = Product("Leche", UnitOfMeasure.Unit, perishable: true);
            var slot = Slot();

            var violations = await _sut.EvaluateAsync(
                "{}", Distribution(slot, FefoLine(0, milk, 2, slot, Lot(milk, 10, Nov(20)), Lot(milk, 10, Nov(12)))), Ct);

            violations.Should().BeEmpty();
        }

        [Fact]
        public void Unknown_parameters_are_rejected()
            => _sut.ValidateParameters("""{"strict":true}""").Should().ContainSingle();
    }

    public sealed class ExpiryAlert
    {
        private readonly ExpiryAlertEvaluator _sut = new();

        [Fact]
        public async Task Lots_expiring_within_n_days_of_pickup_produce_warnings_not_errors()
        {
            var milk = Product("Leche", UnitOfMeasure.Unit, perishable: true);
            var slot = Slot(); // pickup 2026-11-10
            var line = FefoLine(0, milk, 15, slot, Lot(milk, 10, Nov(12)), Lot(milk, 10, Nov(20)));

            var violations = await _sut.EvaluateAsync("""{"daysBeforeExpiry":3}""", Distribution(slot, line), Ct);

            violations.Should().ContainSingle().Which.Should().Match<RuleViolation>(v =>
                v.Code == "expiry.soon" && v.Severity == ViolationSeverity.Warning);
        }

        [Theory]
        [InlineData("{}", "daysBeforeExpiry")]
        [InlineData("""{"daysBeforeExpiri":3}""", "daysBeforeExpiri")]
        [InlineData("""{"daysBeforeExpiry":0}""", "between 1 and")]
        [InlineData("""{"daysBeforeExpiry":"tres"}""", "daysBeforeExpiry")]
        public void Parameter_errors_are_reported(string json, string expectedFragment)
            => _sut.ValidateParameters(json).Should().ContainSingle()
                .Which.Should().Contain(expectedFragment);
    }

    public sealed class PickupWindow
    {
        private const string TueThu = """{"days":["Tuesday","Thursday"],"open":"09:00","close":"14:00","maxSlotCapacity":20}""";
        private readonly PickupWindowEvaluator _sut = new();

        [Fact]
        public async Task Distribution_in_a_slot_inside_the_window_passes()
        {
            var slot = Slot(TuesdayTen);

            (await _sut.EvaluateAsync(TueThu, Distribution(slot), Ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task Slot_on_a_closed_day_is_an_error()
        {
            var wednesday = EvaluationContext.For(new PickupSlotProposal(TuesdayTen.AddDays(1), TuesdayTen.AddDays(1).AddHours(1), 10));

            (await _sut.EvaluateAsync(TueThu, wednesday, Ct)).Should().ContainSingle(v => v.Code == "pickup.outside_window");
        }

        [Fact]
        public async Task Slot_bigger_than_the_maximum_is_an_error()
        {
            var huge = EvaluationContext.For(new PickupSlotProposal(TuesdayTen, TuesdayTen.AddHours(1), 50));

            (await _sut.EvaluateAsync(TueThu, huge, Ct)).Should().ContainSingle(v => v.Code == "pickup.capacity_exceeds_max");
        }

        [Fact]
        public void Does_not_apply_to_donor_drop_offs()
            => _sut.AppliesTo(EvaluationKind.DonorDropOff).Should().BeFalse();
    }

    public sealed class BeneficiaryLimit
    {
        private const string TenKgPerWeek = """{"maxKg":10,"periodDays":7}""";
        private readonly IDistributionHistory _history = Substitute.For<IDistributionHistory>();

        [Theory]
        [InlineData(4, 6, false)] // 4 already + 6 now = 10: exactly at the limit is allowed
        [InlineData(5, 6, true)]  // 11 > 10
        public async Task Already_scheduled_plus_requested_must_not_exceed_the_limit(decimal alreadyKg, decimal requestedKg, bool violates)
        {
            _history.GetKilogramsScheduledAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(alreadyKg);
            var beans = Product();
            var slot = Slot();

            var violations = await new BeneficiaryLimitEvaluator(_history)
                .EvaluateAsync(TenKgPerWeek, Distribution(slot, FefoLine(0, beans, requestedKg, slot, Lot(beans, 50, null))), Ct);

            violations.Any(v => v.Code == "beneficiary.limit_exceeded").Should().Be(violates);
        }

        [Fact]
        public async Task Looks_back_exactly_period_days_from_the_pickup()
        {
            var beans = Product();
            var slot = Slot();

            await new BeneficiaryLimitEvaluator(_history)
                .EvaluateAsync(TenKgPerWeek, Distribution(slot, FefoLine(0, beans, 1, slot, Lot(beans, 50, null))), Ct);

            await _history.Received(1).GetKilogramsScheduledAsync(
                Arg.Any<Guid>(), slot.StartsAt.AddDays(-7), slot.StartsAt, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Products_counted_in_units_do_not_count_as_kilograms()
        {
            var milk = Product("Leche", UnitOfMeasure.Unit, perishable: true);
            var slot = Slot();

            var violations = await new BeneficiaryLimitEvaluator(_history)
                .EvaluateAsync(TenKgPerWeek, Distribution(slot, FefoLine(0, milk, 500, slot, Lot(milk, 500, Nov(20)))), Ct);

            violations.Should().BeEmpty();
            await _history.DidNotReceiveWithAnyArgs().GetKilogramsScheduledAsync(default, default, default, Ct);
        }
    }

    public sealed class DonorDropOffHours
    {
        private const string Weekdays8To12 = """{"days":["Monday","Tuesday","Wednesday","Thursday","Friday"],"open":"08:00","close":"12:00"}""";
        private readonly DonorDropOffHoursEvaluator _sut = new();

        [Fact]
        public async Task Delivery_in_hours_passes_and_after_hours_fails()
        {
            var inHours = EvaluationContext.For(new DonorDropOffProposal(Guid.NewGuid(), TuesdayTen));
            var evening = EvaluationContext.For(new DonorDropOffProposal(Guid.NewGuid(), TuesdayTen.AddHours(8)));

            (await _sut.EvaluateAsync(Weekdays8To12, inHours, Ct)).Should().BeEmpty();
            (await _sut.EvaluateAsync(Weekdays8To12, evening, Ct)).Should().ContainSingle(v => v.Code == "donor.outside_dropoff_hours");
        }
    }
}
