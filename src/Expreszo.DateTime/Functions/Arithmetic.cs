using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes.Functions;

/// <summary>Arithmetic functions (port of <c>functions/arithmetic.ts</c>).</summary>
internal static class Arithmetic
{
    public static Value AddDuration(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        DateUnit u = DateUnits.Parse(a[2]);
        double n = Dt.RequireNumber("addDuration", a[1], "amount");
        return DateUnits.Add(d, n, u);
    }

    public static Value SubtractDuration(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        DateUnit u = DateUnits.Parse(a[2]);
        double n = Dt.RequireNumber("subtractDuration", a[1], "amount");
        return DateUnits.Add(d, -n, u);
    }

    public static Value StartOf(Value[] a, DateTimeOptions o) =>
        DateUnits.StartOf(Normalize.ToDateTime(a[0], o), DateUnits.Parse(a[1]));

    public static Value EndOf(Value[] a, DateTimeOptions o) =>
        DateUnits.EndOf(Normalize.ToDateTime(a[0], o), DateUnits.Parse(a[1]));

    public static Value Diff(Value[] a, DateTimeOptions o) =>
        Value.Number.Of(
            DateUnits.Diff(
                Normalize.ToDateTime(a[0], o),
                Normalize.ToDateTime(a[1], o),
                DateUnits.Parse(a[2])
            )
        );

    public static Value ClampDate(Value[] a, DateTimeOptions o)
    {
        Value.DateTime d = Normalize.ToDateTime(a[0], o);
        Value.DateTime lo = Normalize.ToDateTime(a[1], o);
        Value.DateTime hi = Normalize.ToDateTime(a[2], o);
        if (d.Instant < lo.Instant)
        {
            return lo;
        }
        if (d.Instant > hi.Instant)
        {
            return hi;
        }
        return d;
    }

    public static Value MinDate(Value[] a, DateTimeOptions o) => Reduce(a, o, "minDate", min: true);

    public static Value MaxDate(Value[] a, DateTimeOptions o) => Reduce(a, o, "maxDate", min: false);

    private static Value.DateTime Reduce(Value[] a, DateTimeOptions o, string fn, bool min)
    {
        if (a.Length == 0)
        {
            throw new EvaluationException($"{fn}() requires at least one argument");
        }

        Value.DateTime best = Normalize.ToDateTime(a[0], o);
        for (int i = 1; i < a.Length; i++)
        {
            Value.DateTime next = Normalize.ToDateTime(a[i], o);
            bool take = min ? next.Instant < best.Instant : next.Instant > best.Instant;
            if (take)
            {
                best = next;
            }
        }
        return best;
    }
}
