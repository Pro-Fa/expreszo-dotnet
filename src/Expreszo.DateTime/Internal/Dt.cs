namespace Expreszo.DateTimes.Internal;

/// <summary>
/// Shared conversion helpers between Expreszo <see cref="Value"/>s and the BCL
/// date types. A value is represented as a <see cref="Value.DateTime"/> holding
/// an absolute instant plus a display zone; "wall-clock" means the local
/// date/time as seen in that zone.
/// </summary>
internal static class Dt
{
    /// <summary>Builds a value from an absolute instant, displayed in <paramref name="zone"/>.</summary>
    public static Value.DateTime FromInstant(DateTimeOffset instant, TimeZoneInfo zone) =>
        new(instant.ToUniversalTime(), zone);

    /// <summary>The wall-clock (kind <c>Unspecified</c>) of a value in its own zone.</summary>
    public static DateTime Wall(Value.DateTime d) =>
        DateTime.SpecifyKind(d.Local.DateTime, DateTimeKind.Unspecified);

    /// <summary>Builds a value from a wall-clock interpreted in <paramref name="zone"/>.</summary>
    /// <remarks>
    /// For a non-existent wall time (spring-forward gap) the standard offset is
    /// used, which round-trips to the post-gap instant — the same forward shift
    /// Luxon applies. For an ambiguous wall time (fall-back overlap) the earlier
    /// of the two occurrences is chosen, matching Luxon's default.
    /// </remarks>
    public static Value.DateTime FromWall(DateTime wall, TimeZoneInfo zone)
    {
        DateTime unspec = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        TimeSpan offset = zone.GetUtcOffset(unspec);
        if (zone.IsAmbiguousTime(unspec))
        {
            // Two offsets apply; the earlier occurrence is the one with the
            // larger UTC offset (clocks fall back afterwards). Luxon keeps it.
            TimeSpan[] offsets = zone.GetAmbiguousTimeOffsets(unspec);
            offset = offsets[0] > offsets[1] ? offsets[0] : offsets[1];
        }
        return new Value.DateTime(new DateTimeOffset(unspec, offset).ToUniversalTime(), zone);
    }

    /// <summary>
    /// Parses an ISO 8601 string. When the string carries an offset/Z it is an
    /// absolute instant displayed in <paramref name="zone"/>; otherwise the
    /// wall-clock is interpreted as being in <paramref name="zone"/>.
    /// </summary>
    public static Value.DateTime ParseIso(string s, TimeZoneInfo zone)
    {
        if (
            !DateTime.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime dt
            )
        )
        {
            throw new EvaluationException($"parseISO(): invalid ISO date '{s}'");
        }

        return dt.Kind == DateTimeKind.Unspecified
            ? FromWall(dt, zone)
            : FromInstant(new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero), zone);
    }

    public static Value.DateTime FromMillis(double ms, TimeZoneInfo zone) =>
        FromInstant(DateTimeOffset.FromUnixTimeMilliseconds((long)ms), zone);

    public static Value.DateTime FromUnix(double seconds, TimeZoneInfo zone) =>
        FromInstant(DateTimeOffset.FromUnixTimeSeconds((long)seconds), zone);

    /// <summary>Current instant from the options clock, displayed in the default zone.</summary>
    public static Value.DateTime Now(DateTimeOptions o) =>
        FromInstant(o.NowProvider(), o.DefaultZone);

    /// <summary>
    /// Resolves a zone identifier: <c>utc</c>, <c>local</c> (the supplied
    /// <paramref name="local"/> zone), or an IANA id via the system zone database.
    /// </summary>
    public static TimeZoneInfo ResolveZone(string id, TimeZoneInfo local)
    {
        if (string.Equals(id, "utc", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }
        if (string.Equals(id, "local", StringComparison.OrdinalIgnoreCase))
        {
            return local;
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new EvaluationException($"unknown time zone '{id}'");
        }
    }

    /// <summary>Luxon weekday: 1 = Monday … 7 = Sunday.</summary>
    public static int Weekday(DateTime wall) => ((int)wall.DayOfWeek + 6) % 7 + 1;

    // ---- argument validation (parity with the TS `typeof` guards) ----

    public static double RequireNumber(string fn, Value v, string what)
    {
        if (v is Value.Number n)
        {
            return n.V;
        }
        throw new EvaluationException($"{fn}() expects {what} to be a number; got {v.TypeName()}");
    }

    public static string RequireString(string fn, Value v, string what)
    {
        if (v is Value.String s)
        {
            return s.V;
        }
        throw new EvaluationException($"{fn}() expects {what} to be a string; got {v.TypeName()}");
    }

    /// <summary>True when an optional argument at <paramref name="index"/> is absent or undefined.</summary>
    public static bool Absent(Value[] args, int index) =>
        args.Length <= index || args[index] is Value.Undefined or Value.Null;
}
