using Expreszo.Errors;

namespace Expreszo.Builtins;

/// <summary>
/// Core operator preset: arithmetic, comparison, logical (including the
/// null-coalesce and type-cast operators), bracket indexing, and prefix /
/// postfix unaries. Domain presets (math, string, array, object, utility,
/// type-check) layer on top.
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
        b.AddBinary("+", OperatorTableBuilder.Sync(Add));
        b.AddBinary("-", OperatorTableBuilder.Sync(args => NumericBinary(args, (l, r) => l - r)));
        b.AddBinary("*", OperatorTableBuilder.Sync(args => NumericBinary(args, (l, r) => l * r)));
        b.AddBinary("/", OperatorTableBuilder.Sync(Divide));
        b.AddBinary("%", OperatorTableBuilder.Sync(args => NumericBinary(args, (l, r) => l % r)));
        b.AddBinary("^", OperatorTableBuilder.Sync(args => NumericBinary(args, Math.Pow)));
        b.AddBinary("|", OperatorTableBuilder.Sync(Concat));
    }

    // '+' is stricter than the coercing arithmetic ops: both sides must
    // already be numbers (no string/boolean coercion) so string concatenation
    // doesn't accidentally happen via '+'.
    private static Value Add(Value[] args)
    {
        (Value l, Value r) = (args[0], args[1]);
        if (l is Value.Undefined || r is Value.Undefined)
        {
            return Value.Undefined.Instance;
        }
        if (l is Value.Number ln && r is Value.Number rn)
        {
            return Value.Number.Of(ln.V + rn.V);
        }
        throw new EvaluationException(Messages.CannotAdd(l.TypeName(), r.TypeName()));
    }

    private static Value Divide(Value[] args)
    {
        (Value l, Value r) = (args[0], args[1]);
        if (l is Value.Undefined || r is Value.Undefined)
        {
            return Value.Undefined.Instance;
        }
        double rv = ToNumber(r);
        if (rv == 0)
        {
            throw new EvaluationException(Messages.DivisionByZero());
        }
        return Value.Number.Of(ToNumber(l) / rv);
    }

    // '|' concatenates: array+array produces a new array, otherwise stringifies both sides.
    private static Value Concat(Value[] args)
    {
        (Value l, Value r) = (args[0], args[1]);
        if (l is Value.Array la && r is Value.Array ra)
        {
            return new Value.Array(la.Items.AddRange(ra.Items));
        }
        return new Value.String(ToStringValue(l) + ToStringValue(r));
    }

    // Coercing numeric binary: operands pass through ToNumber (so booleans /
    // numeric strings work), and either side being Undefined short-circuits
    // to Undefined so arithmetic with missing values stays missing.
    private static Value NumericBinary(Value[] args, Func<double, double, double> op)
    {
        if (args[0] is Value.Undefined || args[1] is Value.Undefined)
        {
            return Value.Undefined.Instance;
        }
        return Value.Number.Of(op(ToNumber(args[0]), ToNumber(args[1])));
    }

    // ---------- Comparison ----------

    private static void RegisterComparison(OperatorTableBuilder b)
    {
        b.AddBinary(
            "==",
            OperatorTableBuilder.Sync(args => Value.Boolean.Of(StrictEquals(args[0], args[1])))
        );
        b.AddBinary(
            "!=",
            OperatorTableBuilder.Sync(args => Value.Boolean.Of(!StrictEquals(args[0], args[1])))
        );
        b.AddBinary(
            "<",
            OperatorTableBuilder.Sync(args => CompareOrUndefined(args[0], args[1], (a, b) => a < b))
        );
        b.AddBinary(
            "<=",
            OperatorTableBuilder.Sync(args =>
                CompareOrUndefined(args[0], args[1], (a, b) => a <= b)
            )
        );
        b.AddBinary(
            ">",
            OperatorTableBuilder.Sync(args => CompareOrUndefined(args[0], args[1], (a, b) => a > b))
        );
        b.AddBinary(
            ">=",
            OperatorTableBuilder.Sync(args =>
                CompareOrUndefined(args[0], args[1], (a, b) => a >= b)
            )
        );
        b.AddBinary(
            "in",
            OperatorTableBuilder.Sync(args =>
            {
                Value needle = args[0];
                if (args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                if (args[1] is Value.Array arr)
                {
                    foreach (Value item in arr.Items)
                    {
                        if (StrictEquals(needle, item))
                        {
                            return Value.Boolean.True;
                        }
                    }
                }
                return Value.Boolean.False;
            })
        );
        b.AddBinary(
            "not in",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                if (args[1] is Value.Array arr)
                {
                    foreach (Value item in arr.Items)
                    {
                        if (StrictEquals(args[0], item))
                        {
                            return Value.Boolean.False;
                        }
                    }
                }

                return Value.Boolean.True;
            })
        );
    }

    // ---------- Logical ----------

    private static void RegisterLogical(OperatorTableBuilder b)
    {
        // && / and / || / or are handled with short-circuit in the evaluator
        // itself; these entries still exist so dispatch-by-name finds a function
        // for non-short-circuit call sites (e.g. via the functions catalogue).
        b.AddBinary(
            "and",
            OperatorTableBuilder.Sync(args =>
                Value.Boolean.Of(args[0].IsTruthy() && args[1].IsTruthy())
            )
        );
        b.AddBinary(
            "&&",
            OperatorTableBuilder.Sync(args =>
                Value.Boolean.Of(args[0].IsTruthy() && args[1].IsTruthy())
            )
        );
        b.AddBinary(
            "or",
            OperatorTableBuilder.Sync(args =>
                Value.Boolean.Of(args[0].IsTruthy() || args[1].IsTruthy())
            )
        );
        b.AddBinary(
            "||",
            OperatorTableBuilder.Sync(args =>
                Value.Boolean.Of(args[0].IsTruthy() || args[1].IsTruthy())
            )
        );

        // null coalesce - Null, Undefined, NaN, and Infinity are all nullish.
        b.AddBinary(
            "??",
            OperatorTableBuilder.Sync(args =>
            {
                Value l = args[0];
                bool nullish = l switch
                {
                    Value.Null or Value.Undefined => true,
                    Value.Number n when double.IsNaN(n.V) || double.IsInfinity(n.V) => true,
                    _ => false,
                };
                return nullish ? args[1] : l;
            })
        );

        // type cast
        b.AddBinary(
            "as",
            OperatorTableBuilder.Sync(args =>
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
            })
        );
    }

    // ---------- Unary ----------

    private static void RegisterUnary(OperatorTableBuilder b)
    {
        b.AddUnary(
            "-",
            OperatorTableBuilder.Sync(args =>
                args[0] is Value.Undefined
                    ? (Value)Value.Undefined.Instance
                    : Value.Number.Of(-ToNumber(args[0]))
            )
        );

        b.AddUnary(
            "+",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double v = ToNumber(args[0]);
                return double.IsNaN(v) ? (Value)Value.Undefined.Instance : Value.Number.Of(v);
            })
        );

        b.AddUnary("not", OperatorTableBuilder.Sync(args => Value.Boolean.Of(!args[0].IsTruthy())));

        b.AddUnary(
            "!",
            OperatorTableBuilder.Sync(args =>
            {
                // Postfix factorial. (Prefix `!` means logical-not and is
                // dispatched via the `not` entry; context disambiguates at
                // parse time.)
                if (args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double v = ToNumber(args[0]);
                if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v != Math.Floor(v))
                {
                    throw new EvaluationException(
                        "factorial requires a non-negative finite integer"
                    );
                }

                if (v > EvaluationLimits.MaxFactorialInput)
                {
                    throw new EvaluationException(
                        $"factorial input exceeds {EvaluationLimits.MaxFactorialInput} (beyond double precision)"
                    );
                }

                double r = 1;
                for (int i = 2; i <= v; i++)
                {
                    r *= i;
                }

                return Value.Number.Of(r);
            })
        );
    }

    // ---------- Utility (bracket indexing, assignment handled by evaluator) ----------

    private static void RegisterUtility(OperatorTableBuilder b)
    {
        // Bracket indexing. The evaluator turns `arr[i]` into a call to binaryOps["["].
        b.AddBinary("[", OperatorTableBuilder.Sync(args => BracketIndex(args[0], args[1])));
    }

    private static Value BracketIndex(Value container, Value key) =>
        container switch
        {
            Value.Undefined or Value.Null => Value.Undefined.Instance,
            Value.Array arr => IndexArray(arr, key),
            Value.Object obj => IndexObject(obj, key),
            Value.String s => IndexString(s, key),
            _ => Value.Undefined.Instance,
        };

    private static Value IndexArray(Value.Array arr, Value key)
    {
        Expreszo.Validation.ExpressionValidator.ValidateArrayAccess(arr, key);
        int idx = (int)((Value.Number)key).V;
        return idx >= 0 && idx < arr.Items.Length
            ? arr.Items[idx]
            : Value.Undefined.Instance;
    }

    private static Value IndexObject(Value.Object obj, Value key)
    {
        string k = ToStringValue(key);
        Expreszo.Validation.ExpressionValidator.ValidateMemberAccess(k);
        return obj.Props.TryGetValue(k, out Value? v) ? v : Value.Undefined.Instance;
    }

    private static Value IndexString(Value.String s, Value key)
    {
        // Same strictness as ValidateArrayAccess: reject non-integer, NaN, and
        // Infinity so `s[Infinity]` can't silently coerce through a float-to-int cast.
        if (key is not Value.Number nn)
        {
            throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(key));
        }
        if (nn.V != Math.Floor(nn.V) || double.IsNaN(nn.V) || double.IsInfinity(nn.V))
        {
            throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(nn.V));
        }
        int idx = (int)nn.V;
        return idx >= 0 && idx < s.V.Length
            ? new Value.String(s.V[idx].ToString())
            : Value.Undefined.Instance;
    }

    // ---------- helpers ----------

    internal static double ToNumber(Value v) =>
        v switch
        {
            Value.Number n => n.V,
            Value.Boolean b => b.V ? 1 : 0,
            Value.Null => 0,
            Value.String s => double.TryParse(
                s.V,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double d
            )
                ? d
                : double.NaN,
            _ => double.NaN,
        };

    internal static string ToStringValue(Value v) =>
        v switch
        {
            Value.String s => s.V,
            Value.Number n => n.V.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            Value.Boolean b => b.V ? "true" : "false",
            Value.Null => "null",
            Value.Undefined => "undefined",
            _ => v.ToString() ?? "",
        };

    // JS === semantics: NaN != NaN, -0 == 0, value-compare primitives,
    // reference-compare arrays / objects / functions.
    internal static bool StrictEquals(Value a, Value b) =>
        (a, b) switch
        {
            (Value.Undefined, Value.Undefined) => true,
            (Value.Null, Value.Null) => true,
            (Value.Boolean ba, Value.Boolean bb) => ba.V == bb.V,
            (Value.Number na, Value.Number nb) =>
                !double.IsNaN(na.V) && !double.IsNaN(nb.V) && na.V == nb.V,
            (Value.String sa, Value.String sb) => sa.V == sb.V,
            _ => ReferenceEquals(a, b),
        };

    private static Value CompareOrUndefined(Value a, Value b, Func<double, double, bool> cmp)
    {
        if (a is Value.Undefined || b is Value.Undefined)
        {
            return Value.Undefined.Instance;
        }

        if (a is Value.String sa && b is Value.String sb)
        {
            int r = string.CompareOrdinal(sa.V, sb.V);
            return Value.Boolean.Of(cmp(r, 0));
        }
        return Value.Boolean.Of(cmp(ToNumber(a), ToNumber(b)));
    }
}
