using Expreszo.Errors;

namespace Expreszo.Builtins;

/// <summary>Registers math unary operators, trig operators, and math functions.</summary>
internal static class MathPreset
{
    public static void RegisterInto(OperatorTableBuilder builder)
    {
        RegisterUnary(builder);
        RegisterFunctions(builder);
    }

    // ---------- Unary math (each applies to a single numeric operand) ----------

    private static void RegisterUnary(OperatorTableBuilder b)
    {
        UnaryNum(b, "abs", Math.Abs);
        UnaryNum(b, "ceil", Math.Ceiling);
        UnaryNum(b, "floor", Math.Floor);
        UnaryNum(b, "round", v => Math.Round(v, MidpointRounding.AwayFromZero));
        UnaryNum(b, "sign", v => Math.Sign(v));
        UnaryNum(b, "sqrt", Math.Sqrt);
        UnaryNum(b, "cbrt", Math.Cbrt);
        UnaryNum(b, "trunc", Math.Truncate);
        UnaryNum(b, "exp", Math.Exp);
        UnaryNum(b, "expm1", v => Math.Exp(v) - 1);
        UnaryNum(b, "log", Math.Log);
        UnaryNum(b, "ln", Math.Log);
        UnaryNum(b, "log1p", v => Math.Log(1 + v));
        UnaryNum(b, "log2", Math.Log2);
        UnaryNum(b, "log10", Math.Log10);
        UnaryNum(b, "lg", Math.Log10);

        // trig
        UnaryNum(b, "sin", Math.Sin);
        UnaryNum(b, "cos", Math.Cos);
        UnaryNum(b, "tan", Math.Tan);
        UnaryNum(b, "asin", Math.Asin);
        UnaryNum(b, "acos", Math.Acos);
        UnaryNum(b, "atan", Math.Atan);
        UnaryNum(b, "sinh", Math.Sinh);
        UnaryNum(b, "cosh", Math.Cosh);
        UnaryNum(b, "tanh", Math.Tanh);
        UnaryNum(b, "asinh", Math.Asinh);
        UnaryNum(b, "acosh", Math.Acosh);
        UnaryNum(b, "atanh", Math.Atanh);

        // length: string length, array length, or string-coerced number length.
        b.AddUnary(
            "length",
            OperatorTableBuilder.Sync(args =>
            {
                return args[0] switch
                {
                    Value.Undefined => Value.Undefined.Instance,
                    Value.String s => Value.Number.Of(s.V.Length),
                    Value.Array a => Value.Number.Of(a.Items.Length),
                    Value.Number n => Value.Number.Of(
                        n.V.ToString("R", System.Globalization.CultureInfo.InvariantCulture).Length
                    ),
                    _ => Value.Undefined.Instance,
                };
            })
        );
    }

    private static void UnaryNum(OperatorTableBuilder b, string name, Func<double, double> fn)
    {
        b.AddUnary(
            name,
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double v = CorePreset.ToNumber(args[0]);
                return Value.Number.Of(fn(v));
            })
        );
    }

    // ---------- Math functions ----------

    private static void RegisterFunctions(OperatorTableBuilder b)
    {
        b.AddFunction(
            "atan2",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 2 || args[0] is Value.Undefined || args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                return Value.Number.Of(
                    Math.Atan2(CorePreset.ToNumber(args[0]), CorePreset.ToNumber(args[1]))
                );
            })
        );

        b.AddFunction(
            "clamp",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 3)
                {
                    throw new ExpressionArgumentException("clamp requires 3 arguments");
                }

                if (args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double v = CorePreset.ToNumber(args[0]);
                double lo = CorePreset.ToNumber(args[1]);
                double hi = CorePreset.ToNumber(args[2]);
                return Value.Number.Of(Math.Clamp(v, lo, hi));
            })
        );

        b.AddFunction(
            "fac",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 1 || args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double v = CorePreset.ToNumber(args[0]);
                if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v != Math.Floor(v))
                {
                    throw new EvaluationException("fac requires a non-negative finite integer");
                }

                if (v > EvaluationLimits.MaxFactorialInput)
                {
                    throw new EvaluationException(
                        $"fac input exceeds {EvaluationLimits.MaxFactorialInput} (beyond double precision)"
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

        b.AddFunction(
            "gamma",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 1 || args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double x = CorePreset.ToNumber(args[0]);
                // Lanczos approximation (g = 7, n = 9).
                return Value.Number.Of(Gamma(x));
            })
        );

        b.AddFunction(
            "hypot",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length == 0)
                {
                    return Value.Undefined.Instance;
                }

                double sum = 0;
                foreach (Value a in args)
                {
                    if (a is Value.Undefined)
                    {
                        return Value.Undefined.Instance;
                    }

                    double v = CorePreset.ToNumber(a);
                    sum += v * v;
                }
                return Value.Number.Of(Math.Sqrt(sum));
            })
        );

        b.AddFunction("max", OperatorTableBuilder.Sync(args => MinMax(args, true)));
        b.AddFunction("min", OperatorTableBuilder.Sync(args => MinMax(args, false)));

        b.AddFunction(
            "pow",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 2 || args[0] is Value.Undefined || args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                return Value.Number.Of(
                    Math.Pow(CorePreset.ToNumber(args[0]), CorePreset.ToNumber(args[1]))
                );
            })
        );

        b.AddFunction(
            "random",
            OperatorTableBuilder.Sync(args =>
            {
                double upper =
                    args.Length == 0 || args[0] is Value.Undefined
                        ? 1.0
                        : CorePreset.ToNumber(args[0]);
                // Random.Shared is the thread-safe singleton (.NET 6+). Expression
                // instances are documented as safe to share across threads, so we
                // must not close over a per-parser Random.
                return Value.Number.Of(Random.Shared.NextDouble() * upper);
            })
        );

        b.AddFunction(
            "roundTo",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 1 || args[0] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                double v = CorePreset.ToNumber(args[0]);
                int digits =
                    args.Length >= 2 && args[1] is not Value.Undefined
                        ? (int)CorePreset.ToNumber(args[1])
                        : 0;
                return Value.Number.Of(Math.Round(v, digits, MidpointRounding.AwayFromZero));
            })
        );

        // ---------- statistics ----------

        b.AddFunction(
            "sum",
            OperatorTableBuilder.Sync(args =>
                AggregateNumbers(
                    args,
                    "sum",
                    nums =>
                    {
                        double s = 0;
                        foreach (double n in nums)
                        {
                            s += n;
                        }
                        return s;
                    }
                )
            )
        );

        b.AddFunction(
            "mean",
            OperatorTableBuilder.Sync(args =>
                AggregateNumbers(
                    args,
                    "mean",
                    nums =>
                    {
                        if (nums.Count == 0)
                        {
                            return double.NaN;
                        }
                        double s = 0;
                        foreach (double n in nums)
                        {
                            s += n;
                        }
                        return s / nums.Count;
                    }
                )
            )
        );

        b.AddFunction(
            "median",
            OperatorTableBuilder.Sync(args =>
                AggregateNumbers(
                    args,
                    "median",
                    nums =>
                    {
                        if (nums.Count == 0)
                        {
                            return double.NaN;
                        }

                        double[] sorted = nums.OrderBy(n => n).ToArray();
                        int mid = sorted.Length / 2;
                        return (sorted.Length % 2 == 0)
                            ? (sorted[mid - 1] + sorted[mid]) / 2
                            : sorted[mid];
                    }
                )
            )
        );

        b.AddFunction(
            "variance",
            OperatorTableBuilder.Sync(args => AggregateNumbers(args, "variance", Variance))
        );
        b.AddFunction(
            "stddev",
            OperatorTableBuilder.Sync(args =>
                AggregateNumbers(args, "stddev", nums => Math.Sqrt(Variance(nums)))
            )
        );

        b.AddFunction(
            "mostFrequent",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 1 || args[0] is not Value.Array arr)
                {
                    return Value.Undefined.Instance;
                }

                var counts = new Dictionary<Value, int>();
                var order = new List<Value>();
                foreach (Value item in arr.Items)
                {
                    if (!counts.TryGetValue(item, out int c))
                    {
                        counts[item] = 1;
                        order.Add(item);
                    }
                    else
                    {
                        counts[item] = c + 1;
                    }
                }
                Value? best = null;
                int bestCount = -1;
                foreach (Value item in order)
                {
                    if (counts[item] > bestCount)
                    {
                        best = item;
                        bestCount = counts[item];
                    }
                }
                return best ?? Value.Undefined.Instance;
            })
        );

        b.AddFunction(
            "percentile",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 2 || args[0] is not Value.Array arr)
                {
                    return Value.Undefined.Instance;
                }

                double pRaw = CorePreset.ToNumber(args[1]);
                if (double.IsNaN(pRaw) || double.IsInfinity(pRaw))
                {
                    throw new ExpressionArgumentException(
                        "percentile: p must be finite",
                        "percentile"
                    );
                }

                double p = Math.Clamp(pRaw / 100.0, 0.0, 1.0);
                double[] nums = arr
                    .Items.Select(ToNumberOrNaN)
                    .Where(d => !double.IsNaN(d))
                    .OrderBy(d => d)
                    .ToArray();
                if (nums.Length == 0)
                {
                    return Value.Undefined.Instance;
                }

                double rank = p * (nums.Length - 1);
                int lo = (int)Math.Floor(rank);
                int hi = (int)Math.Ceiling(rank);
                if (lo == hi)
                {
                    return Value.Number.Of(nums[lo]);
                }

                double weight = rank - lo;
                return Value.Number.Of(nums[lo] * (1 - weight) + nums[hi] * weight);
            })
        );
    }

    // ---------- helpers ----------

    private static Value MinMax(Value[] args, bool takeMax)
    {
        if (args.Length == 0)
        {
            return Value.Undefined.Instance;
        }

        IEnumerable<double> numbers;
        if (args.Length == 1 && args[0] is Value.Array a)
        {
            numbers = a.Items.Select(ToNumberOrNaN);
        }
        else
        {
            numbers = args.Select(ToNumberOrNaN);
        }
        double? acc = null;
        foreach (double n in numbers)
        {
            if (double.IsNaN(n))
            {
                continue;
            }

            if (acc is null || (takeMax ? n > acc : n < acc))
            {
                acc = n;
            }
        }
        return acc is null ? Value.Undefined.Instance : Value.Number.Of(acc.Value);
    }

    private static Value AggregateNumbers(
        Value[] args,
        string fnName,
        Func<List<double>, double> aggregate
    )
    {
        if (args.Length < 1 || args[0] is not Value.Array arr)
        {
            return Value.Undefined.Instance;
        }

        var nums = new List<double>(arr.Items.Length);
        foreach (Value item in arr.Items)
        {
            double n = ToNumberOrNaN(item);
            if (!double.IsNaN(n))
            {
                nums.Add(n);
            }
        }
        if (nums.Count == 0)
        {
            return Value.Undefined.Instance;
        }

        double result = aggregate(nums);
        return Value.Number.Of(result);
    }

    private static double Variance(List<double> nums)
    {
        if (nums.Count == 0)
        {
            return double.NaN;
        }

        double mean = 0;
        foreach (double n in nums)
        {
            mean += n;
        }

        mean /= nums.Count;
        double sumSq = 0;
        foreach (double n in nums)
        {
            sumSq += (n - mean) * (n - mean);
        }

        return sumSq / nums.Count;
    }

    private static double ToNumberOrNaN(Value v) =>
        v switch
        {
            Value.Number n => n.V,
            Value.Boolean b => b.V ? 1 : 0,
            _ => double.NaN,
        };

    // Lanczos approximation for the gamma function (g=7, n=9).
    private static readonly double[] GammaCoeffs =
    [
        0.99999999999980993,
        676.5203681218851,
        -1259.1392167224028,
        771.32342877765313,
        -176.61502916214059,
        12.507343278686905,
        -0.13857109526572012,
        9.9843695780195716e-6,
        1.5056327351493116e-7,
    ];

    private static double Gamma(double x)
    {
        if (x < 0.5)
        {
            return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1 - x));
        }
        x -= 1;
        double a = GammaCoeffs[0];
        double t = x + 7.5;
        for (int i = 1; i < GammaCoeffs.Length; i++)
        {
            a += GammaCoeffs[i] / (x + i);
        }
        return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * a;
    }
}
