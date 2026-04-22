using Expreszo.Errors;

namespace Expreszo.Builtins;

/// <summary>
/// Minimal operator / function preset required to evaluate arithmetic,
/// comparisons, logical expressions, and basic utility operations. Phase 5
/// expands this into the full 107-operator / 71-function catalogue.
/// </summary>
internal static class CorePreset
{
    public static void RegisterInto(OperatorTableBuilder builder)
    {
        RegisterArithmetic(builder);
        RegisterComparison(builder);
        RegisterLogical(builder);
        RegisterUnary(builder);
        RegisterUtility(builder);
    }

    // ---------- Binary arithmetic ----------

    private static void RegisterArithmetic(OperatorTableBuilder b)
    {
        b.AddBinary("+", OperatorTableBuilder.Sync(args =>
        {
            var (l, r) = (args[0], args[1]);
            if (l is Value.Undefined || r is Value.Undefined) return Value.Undefined.Instance;
            if (l is Value.Number ln && r is Value.Number rn)
            {
                return Value.Number.Of(ln.V + rn.V);
            }
            throw new EvaluationException(Messages.CannotAdd(l.TypeName(), r.TypeName()));
        }));

        b.AddBinary("-", OperatorTableBuilder.Sync(args =>
        {
            var (l, r) = (args[0], args[1]);
            if (l is Value.Undefined || r is Value.Undefined) return Value.Undefined.Instance;
            return Value.Number.Of(ToNumber(l) - ToNumber(r));
        }));

        b.AddBinary("*", OperatorTableBuilder.Sync(args =>
        {
            var (l, r) = (args[0], args[1]);
            if (l is Value.Undefined || r is Value.Undefined) return Value.Undefined.Instance;
            return Value.Number.Of(ToNumber(l) * ToNumber(r));
        }));

        b.AddBinary("/", OperatorTableBuilder.Sync(args =>
        {
            var (l, r) = (args[0], args[1]);
            if (l is Value.Undefined || r is Value.Undefined) return Value.Undefined.Instance;
            var rv = ToNumber(r);
            if (rv == 0) throw new EvaluationException(Messages.DivisionByZero());
            return Value.Number.Of(ToNumber(l) / rv);
        }));

        b.AddBinary("%", OperatorTableBuilder.Sync(args =>
        {
            var (l, r) = (args[0], args[1]);
            if (l is Value.Undefined || r is Value.Undefined) return Value.Undefined.Instance;
            return Value.Number.Of(ToNumber(l) % ToNumber(r));
        }));

        b.AddBinary("^", OperatorTableBuilder.Sync(args =>
        {
            var (l, r) = (args[0], args[1]);
            if (l is Value.Undefined || r is Value.Undefined) return Value.Undefined.Instance;
            return Value.Number.Of(Math.Pow(ToNumber(l), ToNumber(r)));
        }));

        b.AddBinary("|", OperatorTableBuilder.Sync(args =>
        {
            // concat: arrays + arrays, strings + anything (coerced)
            var (l, r) = (args[0], args[1]);
            if (l is Value.Array la && r is Value.Array ra)
            {
                return new Value.Array(la.Items.AddRange(ra.Items));
            }
            return new Value.String(ToStringValue(l) + ToStringValue(r));
        }));
    }

    // ---------- Comparison ----------

    private static void RegisterComparison(OperatorTableBuilder b)
    {
        b.AddBinary("==", OperatorTableBuilder.Sync(args => Value.Boolean.Of(StrictEquals(args[0], args[1]))));
        b.AddBinary("!=", OperatorTableBuilder.Sync(args => Value.Boolean.Of(!StrictEquals(args[0], args[1]))));
        b.AddBinary("<", OperatorTableBuilder.Sync(args => CompareOrUndefined(args[0], args[1], (a, b) => a < b)));
        b.AddBinary("<=", OperatorTableBuilder.Sync(args => CompareOrUndefined(args[0], args[1], (a, b) => a <= b)));
        b.AddBinary(">", OperatorTableBuilder.Sync(args => CompareOrUndefined(args[0], args[1], (a, b) => a > b)));
        b.AddBinary(">=", OperatorTableBuilder.Sync(args => CompareOrUndefined(args[0], args[1], (a, b) => a >= b)));
        b.AddBinary("in", OperatorTableBuilder.Sync(args =>
        {
            var needle = args[0];
            if (args[1] is Value.Undefined) return Value.Undefined.Instance;
            if (args[1] is Value.Array arr)
            {
                foreach (var item in arr.Items)
                {
                    if (StrictEquals(needle, item)) return Value.Boolean.True;
                }
                return Value.Boolean.False;
            }
            return Value.Boolean.False;
        }));
        b.AddBinary("not in", OperatorTableBuilder.Sync(args =>
        {
            if (args[1] is Value.Undefined) return Value.Undefined.Instance;
            if (args[1] is Value.Array arr)
            {
                foreach (var item in arr.Items)
                {
                    if (StrictEquals(args[0], item)) return Value.Boolean.False;
                }
                return Value.Boolean.True;
            }
            return Value.Boolean.True;
        }));
    }

    // ---------- Logical ----------

    private static void RegisterLogical(OperatorTableBuilder b)
    {
        // && / and / || / or are handled with short-circuit in the evaluator
        // itself; these entries still exist so dispatch-by-name finds a function
        // for non-short-circuit call sites (e.g. via the functions catalogue).
        b.AddBinary("and", OperatorTableBuilder.Sync(args => Value.Boolean.Of(args[0].IsTruthy() && args[1].IsTruthy())));
        b.AddBinary("&&", OperatorTableBuilder.Sync(args => Value.Boolean.Of(args[0].IsTruthy() && args[1].IsTruthy())));
        b.AddBinary("or", OperatorTableBuilder.Sync(args => Value.Boolean.Of(args[0].IsTruthy() || args[1].IsTruthy())));
        b.AddBinary("||", OperatorTableBuilder.Sync(args => Value.Boolean.Of(args[0].IsTruthy() || args[1].IsTruthy())));

        // null coalesce — mirror JS / TS parity: treat Null, Undefined, NaN, Infinity as nullish.
        b.AddBinary("??", OperatorTableBuilder.Sync(args =>
        {
            var l = args[0];
            var nullish = l switch
            {
                Value.Null or Value.Undefined => true,
                Value.Number n when double.IsNaN(n.V) || double.IsInfinity(n.V) => true,
                _ => false,
            };
            return nullish ? args[1] : l;
        }));

        // type cast
        b.AddBinary("as", OperatorTableBuilder.Sync(args =>
        {
            if (args[1] is not Value.String target)
            {
                throw new EvaluationException("cast target must be a string");
            }
            return target.V switch
            {
                "number" => args[0] is Value.Number n ? n : Value.Number.Of(ToNumber(args[0])),
                "int" or "integer" => Value.Number.Of(Math.Round(ToNumber(args[0]))),
                "boolean" => Value.Boolean.Of(args[0].IsTruthy()),
                _ => throw new EvaluationException(Messages.UnsupportedCast(target.V)),
            };
        }));
    }

    // ---------- Unary ----------

    private static void RegisterUnary(OperatorTableBuilder b)
    {
        b.AddUnary("-", OperatorTableBuilder.Sync(args =>
            args[0] is Value.Undefined ? (Value)Value.Undefined.Instance : Value.Number.Of(-ToNumber(args[0]))));

        b.AddUnary("+", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is Value.Undefined) return Value.Undefined.Instance;
            var v = ToNumber(args[0]);
            return double.IsNaN(v) ? (Value)Value.Undefined.Instance : Value.Number.Of(v);
        }));

        b.AddUnary("not", OperatorTableBuilder.Sync(args => Value.Boolean.Of(!args[0].IsTruthy())));

        b.AddUnary("!", OperatorTableBuilder.Sync(args =>
        {
            // Postfix factorial. Prefix usage is rare but semantically "not"
            // in the TS library; context disambiguates at parse time.
            if (args[0] is Value.Undefined) return Value.Undefined.Instance;
            var v = ToNumber(args[0]);
            if (v < 0 || v != Math.Floor(v)) throw new EvaluationException("factorial requires a non-negative integer");
            double r = 1;
            for (var i = 2; i <= v; i++) r *= i;
            return Value.Number.Of(r);
        }));
    }

    // ---------- Utility (bracket indexing, assignment handled by evaluator) ----------

    private static void RegisterUtility(OperatorTableBuilder b)
    {
        // Bracket indexing. The evaluator turns `arr[i]` into a call to binaryOps["["].
        b.AddBinary("[", OperatorTableBuilder.Sync(args =>
        {
            var (container, key) = (args[0], args[1]);
            if (container is Value.Undefined || container is Value.Null) return Value.Undefined.Instance;
            if (container is Value.Array arr)
            {
                if (key is not Value.Number n)
                {
                    throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(key));
                }
                if (n.V != Math.Floor(n.V)) throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(n.V));
                var idx = (int)n.V;
                if (idx < 0 || idx >= arr.Items.Length) return Value.Undefined.Instance;
                return arr.Items[idx];
            }
            if (container is Value.Object obj)
            {
                var k = ToStringValue(key);
                return obj.Props.TryGetValue(k, out var v) ? v : Value.Undefined.Instance;
            }
            if (container is Value.String s)
            {
                if (key is not Value.Number nn) throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(key));
                var idx = (int)nn.V;
                if (idx < 0 || idx >= s.V.Length) return Value.Undefined.Instance;
                return new Value.String(s.V[idx].ToString());
            }
            return Value.Undefined.Instance;
        }));
    }

    // ---------- helpers ----------

    internal static double ToNumber(Value v) => v switch
    {
        Value.Number n => n.V,
        Value.Boolean b => b.V ? 1 : 0,
        Value.Null => 0,
        Value.String s => double.TryParse(s.V, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN,
        _ => double.NaN,
    };

    internal static string ToStringValue(Value v) => v switch
    {
        Value.String s => s.V,
        Value.Number n => n.V.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Value.Boolean b => b.V ? "true" : "false",
        Value.Null => "null",
        Value.Undefined => "undefined",
        _ => v.ToString() ?? "",
    };

    internal static bool StrictEquals(Value a, Value b)
    {
        // JS === semantics: NaN != NaN, -0 == 0, value-compare primitives,
        // reference-compare arrays / objects / functions.
        if (a is Value.Undefined && b is Value.Undefined) return true;
        if (a is Value.Null && b is Value.Null) return true;
        if (a is Value.Boolean ba && b is Value.Boolean bb) return ba.V == bb.V;
        if (a is Value.Number na && b is Value.Number nb)
        {
            if (double.IsNaN(na.V) || double.IsNaN(nb.V)) return false;
            return na.V == nb.V;
        }
        if (a is Value.String sa && b is Value.String sb) return sa.V == sb.V;
        return ReferenceEquals(a, b);
    }

    private static Value CompareOrUndefined(Value a, Value b, Func<double, double, bool> cmp)
    {
        if (a is Value.Undefined || b is Value.Undefined) return Value.Undefined.Instance;
        if (a is Value.String sa && b is Value.String sb)
        {
            var r = string.CompareOrdinal(sa.V, sb.V);
            return Value.Boolean.Of(cmp(r, 0));
        }
        return Value.Boolean.Of(cmp(ToNumber(a), ToNumber(b)));
    }
}
