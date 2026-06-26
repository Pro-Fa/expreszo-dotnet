using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes.Functions;

/// <summary>Format and zone functions (port of <c>functions/format.ts</c>).</summary>
internal static class Format
{
    public static Value FormatDate(Value[] a, DateTimeOptions o)
    {
        if (a[1] is not Value.String pattern)
        {
            throw new EvaluationException("format() pattern must be a string");
        }
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        return new Value.String(
            Dt.Wall(d).ToString(LuxonFormat.Translate(pattern.V), CultureInfo.InvariantCulture)
        );
    }

    public static Value ToISO(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        return new Value.String(
            d.Local.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)
        );
    }

    public static Value ToMillis(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(Normalize.ToDateTime(a[0], o).Instant.ToUnixTimeMilliseconds());

    public static Value ToUnix(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(Normalize.ToDateTime(a[0], o).Instant.ToUnixTimeSeconds());

    public static Value SetZone(Value[] a, DateTimeOptions o)
    {
        if (a[1] is not Value.String zone)
        {
            throw new EvaluationException("setZone() zone must be a string");
        }
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        return new Value.DateTime(d.Instant, Dt.ResolveZone(zone.V, o.DefaultZone));
    }

    public static Value ToUTC(Value[] a, DateTimeOptions o) =>
        new Value.DateTime(Normalize.ToDateTime(a[0], o).Instant, TimeZoneInfo.Utc);

    public static Value ToLocal(Value[] a, DateTimeOptions o) =>
        new Value.DateTime(Normalize.ToDateTime(a[0], o).Instant, o.DefaultZone);

    public static Value ToRelative(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        Value.DateTime baseDate = Dt.Absent(a, 1) ? Dt.Now(o) : Normalize.ToDateTime(a[1], o);

        TimeSpan span = d.Instant - baseDate.Instant;
        bool future = span.Ticks >= 0;
        TimeSpan abs = span.Duration();

        (long n, string unit) = abs.TotalDays >= 1
            ? ((long)abs.TotalDays, "day")
            : abs.TotalHours >= 1
                ? ((long)abs.TotalHours, "hour")
                : abs.TotalMinutes >= 1
                    ? ((long)abs.TotalMinutes, "minute")
                    : ((long)abs.TotalSeconds, "second");

        string plural = n == 1 ? unit : unit + "s";
        return new Value.String(future ? $"in {n} {plural}" : $"{n} {plural} ago");
    }

    public static Value ToRelativeCalendar(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        Value.DateTime baseDate = Dt.Absent(a, 1) ? Dt.Now(o) : Normalize.ToDateTime(a[1], o);

        int days = (Dt.Wall(d).Date - Dt.Wall(baseDate).Date).Days;
        return new Value.String(
            days switch
            {
                0 => "today",
                1 => "tomorrow",
                -1 => "yesterday",
                > 1 => $"in {days} days",
                _ => $"{-days} days ago",
            }
        );
    }
}
