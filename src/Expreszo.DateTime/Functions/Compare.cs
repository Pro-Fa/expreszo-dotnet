using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes.Functions;

/// <summary>Comparison functions (port of <c>functions/compare.ts</c>).</summary>
internal static class Compare
{
    public static Value IsBefore(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(
            Normalize.ToDateTime(a[0], o).Instant < Normalize.ToDateTime(a[1], o).Instant
        );

    public static Value IsAfter(Value[] a, DateTimeOptions o) =>
        Value.Boolean.Of(
            Normalize.ToDateTime(a[0], o).Instant > Normalize.ToDateTime(a[1], o).Instant
        );

    public static Value IsSame(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d1 = Normalize.ToDateTime(a[0], o);
        Value.DateTime d2 = Normalize.ToDateTime(a[1], o);

        if (Dt.Absent(a, 2))
        {
            // No unit: exact equality on Unix milliseconds (parity with the TS
            // isSame, which compares toMillis()).
            return Value.Boolean.Of(
                d1.Instant.ToUnixTimeMilliseconds() == d2.Instant.ToUnixTimeMilliseconds()
            );
        }

        if (a[2] is not Value.String)
        {
            throw new EvaluationException(
                $"isSame() unit must be a string; got {a[2].TypeName()}"
            );
        }

        DateUnit u = DateUnits.Parse(a[2]);
        return Value.Boolean.Of(
            DateUnits.StartOf(d1, u).Instant.UtcTicks == DateUnits.StartOf(d2, u).Instant.UtcTicks
        );
    }

    public static Value IsBetween(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        Value.DateTime lo = Normalize.ToDateTime(a[1], o);
        Value.DateTime hi = Normalize.ToDateTime(a[2], o);
        bool inclusive = Dt.Absent(a, 3) || a[3].IsTruthy();

        DateTimeOffset x = d.Instant;
        return Value.Boolean.Of(
            inclusive
                ? x >= lo.Instant && x <= hi.Instant
                : x > lo.Instant && x < hi.Instant
        );
    }

    public static Value CompareDates(Value[] a, DateTimeOptions o)
    {
        // Parity with the TS compareDates, which compares toMillis().
        long x = Normalize.ToDateTime(a[0], o).Instant.ToUnixTimeMilliseconds();
        long y = Normalize.ToDateTime(a[1], o).Instant.ToUnixTimeMilliseconds();
        return Value.Number.Of(x < y ? -1 : x > y ? 1 : 0);
    }

    public static Value OverlapsRange(Value[] a, DateTimeOptions o)
    {
        DateTimeOffset a1 = Normalize.ToDateTime(a[0], o).Instant;
        DateTimeOffset b1 = Normalize.ToDateTime(a[1], o).Instant;
        DateTimeOffset a2 = Normalize.ToDateTime(a[2], o).Instant;
        DateTimeOffset b2 = Normalize.ToDateTime(a[3], o).Instant;
        return Value.Boolean.Of(a1 <= b2 && a2 <= b1);
    }

    public static Value ContainsDate(Value[] a, DateTimeOptions o)
    {
        DateTimeOffset lo = Normalize.ToDateTime(a[0], o).Instant;
        DateTimeOffset hi = Normalize.ToDateTime(a[1], o).Instant;
        DateTimeOffset d = Normalize.ToDateTime(a[2], o).Instant;
        return Value.Boolean.Of(d >= lo && d <= hi);
    }
}
