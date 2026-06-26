namespace Expreszo.DateTimes;

/// <summary>
/// Configuration for <see cref="ExpreszoDateTimePlugin"/>. Controls the clock
/// the impure functions (<c>now</c>, <c>today</c>, <c>age</c>, the distance and
/// relative-to-now helpers) read, and the "local" zone used when constructing
/// or rendering values that have no explicit zone.
/// </summary>
public sealed class DateTimeOptions
{
    /// <summary>
    /// Source of "now". Defaults to the system clock. Tests inject a fixed value
    /// to make impure functions deterministic (the analog of Luxon's mocked
    /// system time).
    /// </summary>
    public Func<DateTimeOffset> NowProvider { get; init; } = () => DateTimeOffset.Now;

    /// <summary>
    /// The zone treated as "local" — used by <c>now</c>/<c>today</c>/<c>time</c>,
    /// by constructors and parsers that receive no explicit zone, and as the
    /// rendering zone for inspectors. Defaults to the machine's local zone
    /// (the analog of Luxon's <c>local</c> zone).
    /// </summary>
    public TimeZoneInfo DefaultZone { get; init; } = TimeZoneInfo.Local;
}
