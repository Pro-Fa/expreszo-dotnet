namespace Expreszo.DateTimes.Functions;

/// <summary>Distance-from-now functions (port of <c>functions/distance.ts</c>).</summary>
internal static class Distance
{
    private enum Unit
    {
        Days,
        Hours,
        Minutes,
    }

    // Whole-unit distance from now to `d`, truncated toward zero. sign == 1 is
    // "until" (target - now); sign == -1 is "since" (now - target).
    private static Value.Number Compute(Value[] a, DateTimeOptions o, Unit unit, int sign)
    {
        DateTimeOffset target = Normalize.ToDateTime(a[0], o).Instant;
        DateTimeOffset now = o.NowProvider().ToUniversalTime();
        TimeSpan span = sign == 1 ? target - now : now - target;
        double value = unit switch
        {
            Unit.Days => span.TotalDays,
            Unit.Hours => span.TotalHours,
            Unit.Minutes => span.TotalMinutes,
            _ => 0,
        };
        return Value.Number.Of(Math.Truncate(value));
    }

    public static Value DaysUntil(Value[] a, DateTimeOptions o) => Compute(a, o, Unit.Days, 1);

    public static Value DaysSince(Value[] a, DateTimeOptions o) => Compute(a, o, Unit.Days, -1);

    public static Value HoursUntil(Value[] a, DateTimeOptions o) => Compute(a, o, Unit.Hours, 1);

    public static Value HoursSince(Value[] a, DateTimeOptions o) => Compute(a, o, Unit.Hours, -1);

    public static Value MinutesUntil(Value[] a, DateTimeOptions o) => Compute(a, o, Unit.Minutes, 1);

    public static Value MinutesSince(Value[] a, DateTimeOptions o) => Compute(a, o, Unit.Minutes, -1);
}
