namespace Expreszo.DateTimes.Tests;

/// <summary>
/// Shared helpers for the ported date/time suite. Parsers are built with the
/// default zone pinned to UTC so that assertions written against the
/// TZ=UTC-mocked TypeScript suite hold regardless of the host machine zone.
/// The clock is injectable, replacing the TS <c>vi.setSystemTime</c> mock.
/// </summary>
internal static class DateFixture
{
    public static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    /// <summary>A parser whose "now" is fixed to <paramref name="now"/> and zone is UTC.</summary>
    public static Parser ParserAt(DateTimeOffset now) =>
        Parser.WithPlugins(
            [
                new ExpreszoDateTimePlugin(
                    new DateTimeOptions { NowProvider = () => now, DefaultZone = Utc }
                ),
            ]
        );

    /// <summary>
    /// A parser for time-independent tests. "Now" is fixed to the construction
    /// time (UtcNow captured once), zone UTC — these tests don't depend on the clock.
    /// </summary>
    public static Parser ParserUtc() => ParserAt(DateTimeOffset.UtcNow);

    /// <summary>Options used by the direct Normalize.* tests.</summary>
    public static DateTimeOptions UtcOptions { get; } = new() { DefaultZone = Utc };

    // ---- variable injection (the native-input shapes) ----

    public static VariableResolver Vars(params (string Name, object? Value)[] vars)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string name, object? value) in vars)
        {
            dict[name] = value;
        }
        return DateTimeVariables.FromObjects(dict, Utc);
    }

    public static Value Eval(this Parser p, string expr, VariableResolver resolver) =>
        p.Evaluate(expr, null, resolver);

    // ---- value extraction ----

    public static bool Bool(Value v) => ((Value.Boolean)v).V;

    public static double Num(Value v) => ((Value.Number)v).V;

    public static string Str(Value v) => ((Value.String)v).V;

    public static Value.DateTime Date(Value v) => (Value.DateTime)v;

    /// <summary>Luxon <c>.toISODate()</c> equivalent on the value's own (wall-clock) zone.</summary>
    public static string IsoDate(Value v) =>
        Date(v).Local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static long Millis(Value v) => Date(v).Instant.ToUnixTimeMilliseconds();
}
