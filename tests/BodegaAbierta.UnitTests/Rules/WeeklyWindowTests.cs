using BodegaAbierta.Application.Rules;
using static BodegaAbierta.UnitTests.Rules.TestData;

namespace BodegaAbierta.UnitTests.Rules;

public sealed class WeeklyWindowTests
{
    private static readonly WeeklyWindowParameters TueThuMorning = new()
    {
        Days = [DayOfWeek.Tuesday, DayOfWeek.Thursday],
        Open = "09:00",
        Close = "14:00",
    };

    [Fact]
    public void Slot_inside_an_allowed_day_and_hours_is_contained()
        => WeeklyWindow.Contains(TueThuMorning, TuesdayTen, TuesdayTen.AddHours(2)).Should().BeTrue();

    [Fact]
    public void Wrong_day_is_outside()
        => WeeklyWindow.Contains(TueThuMorning, TuesdayTen.AddDays(1), TuesdayTen.AddDays(1).AddHours(1)).Should().BeFalse();

    [Fact]
    public void Ending_after_close_is_outside()
        => WeeklyWindow.Contains(TueThuMorning, TuesdayTen.AddHours(3), TuesdayTen.AddHours(5)).Should().BeFalse();

    [Fact]
    public void Instants_are_converted_to_local_time_first()
    {
        // 16:00 UTC is 10:00 in Mexico City: inside the window even though the UTC clock says 16:00.
        var utc = new DateTimeOffset(2026, 11, 10, 16, 0, 0, TimeSpan.Zero);

        WeeklyWindow.Contains(TueThuMorning, utc, utc).Should().BeTrue();
    }

    [Fact]
    public void Validate_reports_bad_times_order_and_time_zone()
    {
        var errors = WeeklyWindow.Validate(new WeeklyWindowParameters
        {
            Days = [],
            Open = "14:00",
            Close = "9am",
            TimeZone = "Mars/Olympus_Mons",
        }).ToList();

        errors.Should().HaveCount(3)
            .And.Contain(e => e.Contains("'days'", StringComparison.Ordinal))
            .And.Contain(e => e.Contains("'close' must be a time", StringComparison.Ordinal))
            .And.Contain(e => e.Contains("'timeZone'", StringComparison.Ordinal));
    }

    [Fact]
    public void Close_must_be_after_open()
        => WeeklyWindow.Validate(TueThuMorning with { Open = "14:00", Close = "09:00" })
            .Should().ContainSingle(e => e.Contains("after", StringComparison.Ordinal));

    [Fact(Skip = "TODO(Marcus): add ClosedDates to WeeklyWindowParameters")]
    public void Closed_dates_are_outside_the_window()
    {
        // Arrange a window with ClosedDates = [2026-11-10] and assert that the Tuesday 10:00 slot is NOT contained,
        // while Thursday 2026-11-12 10:00 still is.
    }
}
