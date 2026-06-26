using System.Collections.Immutable;
using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes.Functions;

/// <summary>Range / sequence functions (port of <c>functions/range.ts</c>).</summary>
internal static class Ranges
{
    public static Value DateRange(Value[] a, DateTimeOptions o)
    {
        Value.DateTime lo = Normalize.ToDateTime(a[0], o);
        Value.DateTime hi = Normalize.ToDateTime(a[1], o);
        DateUnit u = DateUnits.Parse(a[2]);
        double st = StepArg(a, 3);

        var items = ImmutableArray.CreateBuilder<Value>();
        Value.DateTime cursor = lo;
        while (cursor.Instant < hi.Instant)
        {
            items.Add(cursor);
            cursor = DateUnits.Add(cursor, st, u);
        }
        return new Value.Array(items.ToImmutable());
    }

    public static Value BusinessDaysBetween(Value[] a, DateTimeOptions o)
    {
        Value.DateTime cursor = DateUnits.StartOf(Normalize.ToDateTime(a[0], o), DateUnit.Day);
        Value.DateTime stop = DateUnits.StartOf(Normalize.ToDateTime(a[1], o), DateUnit.Day);
        if (cursor.Instant >= stop.Instant)
        {
            return Value.Number.Of(0);
        }

        int count = 0;
        while (cursor.Instant < stop.Instant)
        {
            if (Dt.Weekday(Dt.Wall(cursor)) < 6)
            {
                count++;
            }
            cursor = DateUnits.Add(cursor, 1, DateUnit.Day);
        }
        return Value.Number.Of(count);
    }

    public static Value WeekdaysBetween(Value[] a, DateTimeOptions o)
    {
        if (
            a[2] is not Value.Number wn
            || wn.V < 1
            || wn.V > 7
            || !double.IsInteger(wn.V)
        )
        {
            throw new EvaluationException(
                $"weekdaysBetween() weekday must be an integer 1..7; got {Describe(a[2])}"
            );
        }
        int weekday = (int)wn.V;

        Value.DateTime cursor = DateUnits.StartOf(Normalize.ToDateTime(a[0], o), DateUnit.Day);
        Value.DateTime stop = DateUnits.StartOf(Normalize.ToDateTime(a[1], o), DateUnit.Day);
        if (cursor.Instant >= stop.Instant)
        {
            return Value.Number.Of(0);
        }

        int count = 0;
        while (cursor.Instant < stop.Instant)
        {
            if (Dt.Weekday(Dt.Wall(cursor)) == weekday)
            {
                count++;
            }
            cursor = DateUnits.Add(cursor, 1, DateUnit.Day);
        }
        return Value.Number.Of(count);
    }

    private static double StepArg(Value[] a, int index)
    {
        if (Dt.Absent(a, index))
        {
            return 1;
        }
        if (a[index] is not Value.Number n || !double.IsFinite(n.V) || n.V <= 0)
        {
            throw new EvaluationException($"step must be a positive number; got {Describe(a[index])}");
        }
        return n.V;
    }

    private static string Describe(Value v) =>
        v switch
        {
            Value.Number n => n.V.ToString(CultureInfo.InvariantCulture),
            Value.String s => s.V,
            _ => v.TypeName(),
        };
}
