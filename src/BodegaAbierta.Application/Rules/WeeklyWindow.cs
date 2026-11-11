using System.Globalization;

namespace BodegaAbierta.Application.Rules;

/// <summary>
/// Days of the week plus opening hours, in the food bank's local time zone. Shared by PickupWindow and
/// DonorDropOffHours. Example: <c>{"days":["Monday","Wednesday"],"open":"09:00","close":"14:00"}</c>.
/// </summary>
public record WeeklyWindowParameters
{
    public required IReadOnlyList<DayOfWeek> Days { get; init; }

    /// <summary>"HH:mm", local time.</summary>
    public required string Open { get; init; }

    /// <summary>"HH:mm", local time.</summary>
    public required string Close { get; init; }

    /// <summary>IANA id. Defaults to <see cref="WeeklyWindow.DefaultTimeZoneId"/>.</summary>
    public string? TimeZone { get; init; }

    // TODO(Marcus) [Phase 4]: add `public IReadOnlyList<DateOnly>? ClosedDates { get; init; }` (holidays such as
    // "2026-12-25"). Validate them in WeeklyWindow.Validate (no duplicates) and make WeeklyWindow.Contains return
    // false when the LOCAL date is a closed date. Both rules that use this record get it for free; the engine
    // does not change. Remove the Skip from WeeklyWindowTests.Closed_dates_are_outside_the_window.
}

public static class WeeklyWindow
{
    /// <summary>Mexico City (UTC-6, no daylight saving time since 2022).</summary>
    public const string DefaultTimeZoneId = "America/Mexico_City";

    private const string TimeFormat = "HH:mm";

    public static IEnumerable<string> Validate(WeeklyWindowParameters window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Days.Count == 0)
        {
            yield return "'days' must contain at least one day.";
        }
        else if (window.Days.Distinct().Count() != window.Days.Count)
        {
            yield return "'days' contains duplicates.";
        }

        var openOk = TryParseTime(window.Open, out var open);
        var closeOk = TryParseTime(window.Close, out var close);
        if (!openOk)
        {
            yield return $"'open' must be a time in {TimeFormat} format.";
        }

        if (!closeOk)
        {
            yield return $"'close' must be a time in {TimeFormat} format.";
        }

        if (openOk && closeOk && close <= open)
        {
            yield return "'close' must be after 'open'.";
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(window.TimeZone ?? DefaultTimeZoneId, out _))
        {
            yield return $"'timeZone' '{window.TimeZone}' is not a known IANA time zone.";
        }
    }

    /// <summary>
    /// True when [start, end] falls inside one allowed day, between open and close, in local time.
    /// For a single instant pass the same value twice.
    /// </summary>
    public static bool Contains(WeeklyWindowParameters window, DateTimeOffset start, DateTimeOffset end)
    {
        ArgumentNullException.ThrowIfNull(window);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(window.TimeZone ?? DefaultTimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(start, zone);
        var localEnd = TimeZoneInfo.ConvertTime(end, zone);

        if (!TryParseTime(window.Open, out var open) || !TryParseTime(window.Close, out var close))
        {
            return false;
        }

        return localStart.Date == localEnd.Date
            && window.Days.Contains(localStart.DayOfWeek)
            && TimeOnly.FromDateTime(localStart.DateTime) >= open
            && TimeOnly.FromDateTime(localEnd.DateTime) <= close;
    }

    public static string Describe(WeeklyWindowParameters window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return $"{string.Join(", ", window.Days)} {window.Open}-{window.Close} ({window.TimeZone ?? DefaultTimeZoneId})";
    }

    /// <summary>Today's calendar date in the given (or default) time zone.</summary>
    public static DateOnly LocalToday(DateTimeOffset now, string? timeZoneId = null)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? DefaultTimeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    private static bool TryParseTime(string? value, out TimeOnly time)
        => TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
