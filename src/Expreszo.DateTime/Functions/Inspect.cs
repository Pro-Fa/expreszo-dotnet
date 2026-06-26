using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes.Functions;

/// <summary>Inspection functions (port of <c>functions/inspect.ts</c>).</summary>
internal static class Inspect
{
    private static DateTime Wall(Value[] a, DateTimeOptions o) =>
        Dt.Wall(Normalize.ToDateTime(a[0], o));

    // ---- calendar parts ----

    public static Value Year(Value[] a, DateTimeOptions o) => Value.Number.Of(Wall(a, o).Year);

    public static Value Month(Value[] a, DateTimeOptions o) => Value.Number.Of(Wall(a, o).Month);

    public static Value Day(Value[] a, DateTimeOptions o) => Value.Number.Of(Wall(a, o).Day);

    public static Value Hour(Value[] a, DateTimeOptions o) => Value.Number.Of(Wall(a, o).Hour);

    public static Value Minute(Value[] a, DateTimeOptions o) => Value.Number.Of(Wall(a, o).Minute);

    public static Value Second(Value[] a, DateTimeOptions o) => Value.Number.Of(Wall(a, o).Second);

    public static Value Millisecond(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(Wall(a, o).Millisecond);

    public static Value DayOfWeek(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(Dt.Weekday(Wall(a, o)));

    public static Value DayOfYear(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(Wall(a, o).DayOfYear);

    public static Value WeekOfYear(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(ISOWeek.GetWeekOfYear(Wall(a, o)));

    public static Value DaysInMonth(Value[] a, DateTimeOptions o)
    {
        DateTime w = Wall(a, o);
        return Value.Number.Of(DateTime.DaysInMonth(w.Year, w.Month));
    }

    public static Value Quarter(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(((Wall(a, o).Month - 1) / 3) + 1);

    public static Value IsoWeekYear(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(ISOWeek.GetYear(Wall(a, o)));

    public static Value IsLeapYear(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(DateTime.IsLeapYear(Wall(a, o).Year));

    public static Value DaysInYear(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(DateTime.IsLeapYear(Wall(a, o).Year) ? 366 : 365);

    public static Value WeeksInYear(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(ISOWeek.GetWeeksInYear(ISOWeek.GetYear(Wall(a, o))));

    public static Value IsDST(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        return Value.Boolean.Of(d.Zone.IsDaylightSavingTime(d.Instant));
    }

    public static Value OffsetMinutes(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        return Value.Number.Of(d.Zone.GetUtcOffset(d.Instant).TotalMinutes);
    }

    public static Value OffsetHours(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        return Value.Number.Of(d.Zone.GetUtcOffset(d.Instant).TotalMinutes / 60);
    }

    public static Value ZoneName(Value[] a, DateTimeOptions o) =>
        new Value.String(Normalize.ToDateTime(a[0], o).Zone.Id);

    public static Value IsWeekend(Value[] a, DateTimeOptions o)
    {
        int wd = Dt.Weekday(Wall(a, o));
        return Value.Boolean.Of(wd == 6 || wd == 7);
    }

    public static Value IsWeekday(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(!((Value.Boolean)IsWeekend(a, o)).V);

    public static Value IsValid(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(
            a[0] switch
            {
                Value.DateTime => true,
                Value.Number n => double.IsFinite(n.V),
                Value.String s => DateTime.TryParse(
                    s.V,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _
                ),
                _ => false,
            }
        );

    // ---- relative-to-now predicates ----

    private static bool SameDay(Value.DateTime a, Value.DateTime b) =>
        Dt.Wall(a).Date == Dt.Wall(b).Date;

    public static Value IsToday(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(SameDay(Normalize.ToDateTime(a[0], o), Dt.Now(o)));

    public static Value IsYesterday(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(
            SameDay(Normalize.ToDateTime(a[0], o), DateUnits.Add(Dt.Now(o), -1, DateUnit.Day))
        );

    public static Value IsTomorrow(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(
            SameDay(Normalize.ToDateTime(a[0], o), DateUnits.Add(Dt.Now(o), 1, DateUnit.Day))
        );

    public static Value IsThisWeek(Value[] a, DateTimeOptions o)
    {
        DateTime d = Wall(a, o);
        DateTime now = Dt.Wall(Dt.Now(o));
        return Value.Boolean.Of(
            ISOWeek.GetYear(d) == ISOWeek.GetYear(now)
                && ISOWeek.GetWeekOfYear(d) == ISOWeek.GetWeekOfYear(now)
        );
    }

    public static Value IsThisMonth(Value[] a, DateTimeOptions o)
    {
        DateTime d = Wall(a, o);
        DateTime now = Dt.Wall(Dt.Now(o));
        return Value.Boolean.Of(d.Year == now.Year && d.Month == now.Month);
    }

    public static Value IsThisYear(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(Wall(a, o).Year == Dt.Wall(Dt.Now(o)).Year);

    public static Value IsInPast(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(Normalize.ToDateTime(a[0], o).Instant < Dt.Now(o).Instant);

    public static Value IsInFuture(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(Normalize.ToDateTime(a[0], o).Instant > Dt.Now(o).Instant);

    public static Value Age(Value[] a, DateTimeOptions o)
    {
        DateTime birth = Wall(a, o);
        DateTime now = Dt.Wall(Dt.Now(o));
        int years = now.Year - birth.Year;
        if (birth.AddYears(years) > now)
        {
            years--;
        }
        return Value.Number.Of(years < 0 ? 0 : years);
    }
}
