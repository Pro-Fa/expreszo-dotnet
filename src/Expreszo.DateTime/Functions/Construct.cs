using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes.Functions;

/// <summary>Construction functions (port of <c>functions/construct.ts</c>).</summary>
internal static class Construct
{
    public static Value Now(Value[] a, DateTimeOptions o) => Dt.Now(o);

    public static Value Today(Value[] a, DateTimeOptions o) =>
        DateUnits.StartOf(Dt.Now(o), DateUnit.Day);

    public static Value Yesterday(Value[] a, DateTimeOptions o) =>
        DateUnits.Add(DateUnits.StartOf(Dt.Now(o), DateUnit.Day), -1, DateUnit.Day);

    public static Value Tomorrow(Value[] a, DateTimeOptions o) =>
        DateUnits.Add(DateUnits.StartOf(Dt.Now(o), DateUnit.Day), 1, DateUnit.Day);

    public static Value ParseISO(Value[] a, DateTimeOptions o)
    {
        string s = Dt.RequireString("parseISO", a[0], "input");
        return Dt.ParseIso(s, o.DefaultZone);
    }

    public static Value ParseDate(Value[] a, DateTimeOptions o)
    {
        if (a[0] is not Value.String input || a[1] is not Value.String fmt)
        {
            throw new EvaluationException("parseDate() expects (string, string, zone?)");
        }

        if (
            !DateTime.TryParseExact(
                input.V,
                LuxonFormat.Translate(fmt.V),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime wall
            )
        )
        {
            throw new EvaluationException(
                $"parseDate(): '{input.V}' does not match format '{fmt.V}'"
            );
        }

        TimeZoneInfo zone = Dt.Absent(a, 2)
            ? o.DefaultZone
            : Dt.ResolveZone(Dt.RequireString("parseDate", a[2], "zone"), o.DefaultZone);

        return Dt.FromWall(wall, zone);
    }

    public static Value FromMillis(Value[] a, DateTimeOptions o) =>
        Dt.FromMillis(Dt.RequireNumber("fromMillis", a[0], "ms"), o.DefaultZone);

    public static Value FromUnix(Value[] a, DateTimeOptions o) =>
        Dt.FromUnix(Dt.RequireNumber("fromUnix", a[0], "seconds"), o.DefaultZone);

    public static Value DateTimeFn(Value[] a, DateTimeOptions o)
    {
        double year = Dt.RequireNumber("dateTime", a[0], "year");
        double month = Dt.RequireNumber("dateTime", a[1], "month");
        double day = Dt.RequireNumber("dateTime", a[2], "day");
        int hour = OptionalInt(a, 3);
        int minute = OptionalInt(a, 4);
        int second = OptionalInt(a, 5);

        var wall = new DateTime((int)year, (int)month, (int)day, hour, minute, second);
        return Dt.FromWall(wall, o.DefaultZone);
    }

    public static Value Date(Value[] a, DateTimeOptions o) =>
        DateTimeFn([a[0], a[1], a[2]], o);

    public static Value Time(Value[] a, DateTimeOptions o)
    {
        int hour = (int)Dt.RequireNumber("time", a[0], "hour");
        int minute = (int)Dt.RequireNumber("time", a[1], "minute");
        int second = OptionalInt(a, 2);
        int millisecond = OptionalInt(a, 3);

        DateTime nowWall = Dt.Wall(Dt.Now(o));
        var wall = new DateTime(
            nowWall.Year,
            nowWall.Month,
            nowWall.Day,
            hour,
            minute,
            second,
            millisecond
        );
        return Dt.FromWall(wall, o.DefaultZone);
    }

    // Optional numeric component: a non-number (or absent) argument defaults to
    // 0, matching the TS `typeof x === 'number' ? x : 0` guards.
    private static int OptionalInt(Value[] a, int index) =>
        index < a.Length && a[index] is Value.Number n ? (int)n.V : 0;
}
