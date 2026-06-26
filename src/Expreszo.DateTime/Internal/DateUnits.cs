namespace Expreszo.DateTimes.Internal;

internal enum DateUnit
{
    Year,
    Quarter,
    Month,
    Week,
    Day,
    Hour,
    Minute,
    Second,
    Millisecond,
}

/// <summary>
/// Unit vocabulary and the calendar/duration math used by arithmetic, range,
/// and comparison functions. The accepted spellings match Luxon exactly
/// (case-sensitive, singular and plural).
/// </summary>
internal static class DateUnits
{
    public static DateUnit Parse(Value v)
    {
        if (v is Value.String s)
        {
            switch (s.V)
            {
                case "year":
                case "years":
                    return DateUnit.Year;
                case "quarter":
                case "quarters":
                    return DateUnit.Quarter;
                case "month":
                case "months":
                    return DateUnit.Month;
                case "week":
                case "weeks":
                    return DateUnit.Week;
                case "day":
                case "days":
                    return DateUnit.Day;
                case "hour":
                case "hours":
                    return DateUnit.Hour;
                case "minute":
                case "minutes":
                    return DateUnit.Minute;
                case "second":
                case "seconds":
                    return DateUnit.Second;
                case "millisecond":
                case "milliseconds":
                    return DateUnit.Millisecond;
            }
        }

        throw new EvaluationException(
            "unit must be one of: year(s), quarter(s), month(s), week(s), day(s), hour(s), "
                + $"minute(s), second(s), millisecond(s); got {Describe(v)}"
        );
    }

    private static string Describe(Value v) =>
        v switch
        {
            Value.String s => s.V,
            _ => v.TypeName(),
        };

    public static Value.DateTime Add(Value.DateTime d, double n, DateUnit u)
    {
        TimeZoneInfo zone = d.Zone;
        DateTime wall = Dt.Wall(d);
        return u switch
        {
            DateUnit.Year => Dt.FromWall(wall.AddYears((int)n), zone),
            DateUnit.Quarter => Dt.FromWall(wall.AddMonths((int)(n * 3)), zone),
            DateUnit.Month => Dt.FromWall(wall.AddMonths((int)n), zone),
            DateUnit.Week => Dt.FromWall(wall.AddDays(n * 7), zone),
            DateUnit.Day => Dt.FromWall(wall.AddDays(n), zone),
            DateUnit.Hour => Dt.FromInstant(d.Instant.AddHours(n), zone),
            DateUnit.Minute => Dt.FromInstant(d.Instant.AddMinutes(n), zone),
            DateUnit.Second => Dt.FromInstant(d.Instant.AddSeconds(n), zone),
            DateUnit.Millisecond => Dt.FromInstant(d.Instant.AddMilliseconds(n), zone),
            _ => d,
        };
    }

    public static Value.DateTime StartOf(Value.DateTime d, DateUnit u)
    {
        DateTime w = Dt.Wall(d);
        DateTime truncated = u switch
        {
            DateUnit.Year => new DateTime(w.Year, 1, 1),
            DateUnit.Quarter => new DateTime(w.Year, ((w.Month - 1) / 3) * 3 + 1, 1),
            DateUnit.Month => new DateTime(w.Year, w.Month, 1),
            DateUnit.Week => w.Date.AddDays(-((Dt.Weekday(w) - 1))),
            DateUnit.Day => w.Date,
            DateUnit.Hour => new DateTime(w.Year, w.Month, w.Day, w.Hour, 0, 0),
            DateUnit.Minute => new DateTime(w.Year, w.Month, w.Day, w.Hour, w.Minute, 0),
            DateUnit.Second => new DateTime(w.Year, w.Month, w.Day, w.Hour, w.Minute, w.Second),
            DateUnit.Millisecond => w,
            _ => w,
        };
        return Dt.FromWall(truncated, d.Zone);
    }

    public static Value.DateTime EndOf(Value.DateTime d, DateUnit u)
    {
        Value.DateTime start = StartOf(d, u);
        Value.DateTime next = Add(start, 1, u);
        return Dt.FromInstant(next.Instant.AddMilliseconds(-1), d.Zone);
    }

    public static double Diff(Value.DateTime a, Value.DateTime b, DateUnit u)
    {
        TimeSpan span = a.Instant - b.Instant;
        return u switch
        {
            DateUnit.Day => span.TotalDays,
            DateUnit.Week => span.TotalDays / 7,
            DateUnit.Hour => span.TotalHours,
            DateUnit.Minute => span.TotalMinutes,
            DateUnit.Second => span.TotalSeconds,
            DateUnit.Millisecond => span.TotalMilliseconds,
            DateUnit.Month => DiffMonths(Dt.Wall(a), Dt.Wall(b)),
            DateUnit.Quarter => DiffMonths(Dt.Wall(a), Dt.Wall(b)) / 3,
            DateUnit.Year => DiffMonths(Dt.Wall(a), Dt.Wall(b)) / 12,
            _ => 0,
        };
    }

    // Signed fractional month difference (a - b), calendar-aware (Luxon's
    // Duration.as('months') semantics).
    private static double DiffMonths(DateTime a, DateTime b)
    {
        int sign = a >= b ? 1 : -1;
        DateTime hi = sign > 0 ? a : b;
        DateTime lo = sign > 0 ? b : a;

        int months = ((hi.Year - lo.Year) * 12) + (hi.Month - lo.Month);
        DateTime anchor = lo.AddMonths(months);
        if (anchor > hi)
        {
            months--;
            anchor = lo.AddMonths(months);
        }

        DateTime next = lo.AddMonths(months + 1);
        double frac = (hi - anchor).Ticks / (double)(next - anchor).Ticks;
        return sign * (months + frac);
    }
}
