using Expreszo.DateTimes.Functions;

namespace Expreszo.DateTimes;

/// <summary>
/// Adds Luxon-equivalent date/time functions to an Expreszo
/// <see cref="Parser"/>. Register via the parser:
/// <code>var parser = Parser.WithPlugins([new ExpreszoDateTimePlugin()]);</code>
/// Values are represented as <see cref="Value.DateTime"/> (a zone-aware instant)
/// and flow through the core <c>==</c>/<c>!=</c>/<c>&lt;</c>/<c>&gt;</c> operators.
/// </summary>
public sealed class ExpreszoDateTimePlugin : IExpreszoPlugin
{
    private readonly DateTimeOptions _options;

    /// <summary>Creates the plugin with the given options (clock + default zone).</summary>
    public ExpreszoDateTimePlugin(DateTimeOptions? options = null) =>
        _options = options ?? new DateTimeOptions();

    /// <inheritdoc />
    public string Name => "@pro-fa/expreszo-datetime";

    /// <inheritdoc />
    public string Version => "0.2.0";

    /// <inheritdoc />
    public void Register(IPluginRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        foreach ((string name, DtFn impl) in Registry)
        {
            DateTimeOptions options = _options;
            string fnName = name;
            registration.AddFunction(
                fnName,
                (args, _) => ValueTask.FromResult(Invoke(fnName, impl, args, options))
            );
        }
    }

    // Under-applying a function (e.g. `year()` with no argument) indexes past
    // the args array; surface that as a controlled EvaluationException naming
    // the function rather than letting a raw IndexOutOfRangeException escape.
    private static Value Invoke(string name, DtFn impl, Value[] args, DateTimeOptions options)
    {
        try
        {
            return impl(args, options);
        }
        catch (IndexOutOfRangeException)
        {
            throw new EvaluationException($"{name}() called with too few arguments");
        }
    }

    /// <summary>A pure date/time function: maps arguments to a result value.</summary>
    private delegate Value DtFn(Value[] args, DateTimeOptions options);

    // Order mirrors DATETIME_FUNCTIONS in the TypeScript plugin.ts.
    private static readonly (string Name, DtFn Impl)[] Registry =
    [
        // Construction
        ("now", Construct.Now),
        ("today", Construct.Today),
        ("yesterday", Construct.Yesterday),
        ("tomorrow", Construct.Tomorrow),
        ("parseISO", Construct.ParseISO),
        ("parseDate", Construct.ParseDate),
        ("fromMillis", Construct.FromMillis),
        ("fromUnix", Construct.FromUnix),
        ("dateTime", Construct.DateTimeFn),
        ("date", Construct.Date),
        ("time", Construct.Time),
        // Inspection — calendar parts
        ("year", Inspect.Year),
        ("month", Inspect.Month),
        ("day", Inspect.Day),
        ("hour", Inspect.Hour),
        ("minute", Inspect.Minute),
        ("second", Inspect.Second),
        ("millisecond", Inspect.Millisecond),
        ("dayOfWeek", Inspect.DayOfWeek),
        ("dayOfYear", Inspect.DayOfYear),
        ("weekOfYear", Inspect.WeekOfYear),
        ("daysInMonth", Inspect.DaysInMonth),
        ("quarter", Inspect.Quarter),
        ("isoWeekYear", Inspect.IsoWeekYear),
        ("isLeapYear", Inspect.IsLeapYear),
        ("daysInYear", Inspect.DaysInYear),
        ("weeksInYear", Inspect.WeeksInYear),
        ("isDST", Inspect.IsDST),
        ("offsetMinutes", Inspect.OffsetMinutes),
        ("offsetHours", Inspect.OffsetHours),
        ("zoneName", Inspect.ZoneName),
        ("isWeekend", Inspect.IsWeekend),
        ("isWeekday", Inspect.IsWeekday),
        ("isValid", Inspect.IsValid),
        // Inspection — relative-to-now
        ("isToday", Inspect.IsToday),
        ("isYesterday", Inspect.IsYesterday),
        ("isTomorrow", Inspect.IsTomorrow),
        ("isThisWeek", Inspect.IsThisWeek),
        ("isThisMonth", Inspect.IsThisMonth),
        ("isThisYear", Inspect.IsThisYear),
        ("isInPast", Inspect.IsInPast),
        ("isInFuture", Inspect.IsInFuture),
        ("age", Inspect.Age),
        // Arithmetic
        ("addDuration", Arithmetic.AddDuration),
        ("subtractDuration", Arithmetic.SubtractDuration),
        ("startOf", Arithmetic.StartOf),
        ("endOf", Arithmetic.EndOf),
        ("diff", Arithmetic.Diff),
        ("clampDate", Arithmetic.ClampDate),
        ("minDate", Arithmetic.MinDate),
        ("maxDate", Arithmetic.MaxDate),
        // Comparison
        ("isBefore", Compare.IsBefore),
        ("isAfter", Compare.IsAfter),
        ("isSame", Compare.IsSame),
        ("isBetween", Compare.IsBetween),
        ("compareDates", Compare.CompareDates),
        ("overlapsRange", Compare.OverlapsRange),
        ("containsDate", Compare.ContainsDate),
        // Range / sequence
        ("dateRange", Ranges.DateRange),
        ("businessDaysBetween", Ranges.BusinessDaysBetween),
        ("weekdaysBetween", Ranges.WeekdaysBetween),
        // Distance from now
        ("daysUntil", Distance.DaysUntil),
        ("daysSince", Distance.DaysSince),
        ("hoursUntil", Distance.HoursUntil),
        ("hoursSince", Distance.HoursSince),
        ("minutesUntil", Distance.MinutesUntil),
        ("minutesSince", Distance.MinutesSince),
        // Format / zone
        ("format", Format.FormatDate),
        ("toISO", Format.ToISO),
        ("toMillis", Format.ToMillis),
        ("toUnix", Format.ToUnix),
        ("setZone", Format.SetZone),
        ("toUTC", Format.ToUTC),
        ("toLocal", Format.ToLocal),
        ("toRelative", Format.ToRelative),
        ("toRelativeCalendar", Format.ToRelativeCalendar),
    ];

    /// <summary>Names of every function this plugin registers (analog of <c>DATETIME_FUNCTIONS</c>).</summary>
    public static IReadOnlyList<string> FunctionNames { get; } =
        Registry.Select(r => r.Name).ToArray();
}
