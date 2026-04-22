using System.Collections.Immutable;
using Expreszo.Errors;

namespace Expreszo.Builtins;

/// <summary>Registers the array-category functions. Higher-order functions
/// (map / filter / fold / find / some / every / sort / groupBy / countBy)
/// invoke caller-supplied lambdas.</summary>
internal static class ArrayPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        b.AddFunction("count", OperatorTableBuilder.Sync(args =>
            args[0] is Value.Array a ? Value.Number.Of(a.Items.Length) : Value.Undefined.Instance));

        HigherOrder(b, "filter", async (arr, fn, ctx) =>
        {
            var result = ImmutableArray.CreateBuilder<Value>();
            for (var i = 0; i < arr.Length; i++)
            {
                var ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                if (ok.IsTruthy()) result.Add(arr[i]);
            }
            return new Value.Array(result.ToImmutable());
        });

        HigherOrderWithInit(b, "fold", async (arr, init, fn, ctx) =>
        {
            var acc = init;
            for (var i = 0; i < arr.Length; i++)
            {
                acc = await fn.Invoke([acc, arr[i], Value.Number.Of(i)], ctx);
            }
            return acc;
        });

        HigherOrderWithInit(b, "reduce", async (arr, init, fn, ctx) =>
        {
            var acc = init;
            for (var i = 0; i < arr.Length; i++)
            {
                acc = await fn.Invoke([acc, arr[i], Value.Number.Of(i)], ctx);
            }
            return acc;
        });

        HigherOrder(b, "find", async (arr, fn, ctx) =>
        {
            for (var i = 0; i < arr.Length; i++)
            {
                var ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                if (ok.IsTruthy()) return arr[i];
            }
            return Value.Undefined.Instance;
        });

        HigherOrder(b, "some", async (arr, fn, ctx) =>
        {
            for (var i = 0; i < arr.Length; i++)
            {
                var ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                if (ok.IsTruthy()) return Value.Boolean.True;
            }
            return Value.Boolean.False;
        });

        HigherOrder(b, "every", async (arr, fn, ctx) =>
        {
            for (var i = 0; i < arr.Length; i++)
            {
                var ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                if (!ok.IsTruthy()) return Value.Boolean.False;
            }
            return Value.Boolean.True;
        });

        HigherOrder(b, "map", async (arr, fn, ctx) =>
        {
            var result = ImmutableArray.CreateBuilder<Value>(arr.Length);
            for (var i = 0; i < arr.Length; i++)
            {
                result.Add(await fn.Invoke([arr[i], Value.Number.Of(i)], ctx));
            }
            return new Value.Array(result.ToImmutable());
        });

        HigherOrder(b, "groupBy", async (arr, fn, ctx) =>
        {
            var dict = new Dictionary<string, List<Value>>(StringComparer.Ordinal);
            var order = new List<string>();
            for (var i = 0; i < arr.Length; i++)
            {
                var key = CorePreset.ToStringValue(await fn.Invoke([arr[i], Value.Number.Of(i)], ctx));
                if (!dict.TryGetValue(key, out var bucket))
                {
                    bucket = new List<Value>();
                    dict[key] = bucket;
                    order.Add(key);
                }
                bucket.Add(arr[i]);
            }
            var result = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var k in order) result[k] = new Value.Array([.. dict[k]]);
            return Value.Object.From(result);
        });

        HigherOrder(b, "countBy", async (arr, fn, ctx) =>
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            for (var i = 0; i < arr.Length; i++)
            {
                var key = CorePreset.ToStringValue(await fn.Invoke([arr[i], Value.Number.Of(i)], ctx));
                if (!counts.TryGetValue(key, out var c))
                {
                    counts[key] = 1;
                    order.Add(key);
                }
                else
                {
                    counts[key] = c + 1;
                }
            }
            var result = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var k in order) result[k] = Value.Number.Of(counts[k]);
            return Value.Object.From(result);
        });

        b.AddFunction("sort", (args, ctx) => SortAsync(args, ctx));

        b.AddFunction("unique", OperatorTableBuilder.Sync(args => Dedupe(args)));
        b.AddFunction("distinct", OperatorTableBuilder.Sync(args => Dedupe(args)));

        b.AddFunction("indexOf", OperatorTableBuilder.Sync(args =>
        {
            var (haystack, needle) = (args[0], args[1]);
            if (haystack is Value.Array a)
            {
                for (var i = 0; i < a.Items.Length; i++)
                {
                    if (CorePreset.StrictEquals(a.Items[i], needle)) return Value.Number.Of(i);
                }
                return Value.Number.Of(-1);
            }
            if (haystack is Value.String s)
            {
                if (needle is Value.String n) return Value.Number.Of(s.V.IndexOf(n.V, StringComparison.Ordinal));
            }
            return Value.Number.Of(-1);
        }));

        b.AddFunction("join", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Array a || args[1] is not Value.String sep) return Value.Undefined.Instance;
            return new Value.String(string.Join(sep.V, a.Items.Select(CorePreset.ToStringValue)));
        }));

        b.AddFunction("range", OperatorTableBuilder.Sync(args =>
        {
            var start = CorePreset.ToNumber(args[0]);
            var end = CorePreset.ToNumber(args[1]);
            var step = args.Length >= 3 && args[2] is not Value.Undefined ? CorePreset.ToNumber(args[2]) : 1;
            if (step == 0) throw new EvaluationException("range step cannot be zero");
            var result = ImmutableArray.CreateBuilder<Value>();
            if (step > 0)
            {
                for (var x = start; x < end; x += step) result.Add(Value.Number.Of(x));
            }
            else
            {
                for (var x = start; x > end; x += step) result.Add(Value.Number.Of(x));
            }
            return new Value.Array(result.ToImmutable());
        }));

        b.AddFunction("chunk", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Array a) return Value.Undefined.Instance;
            var size = (int)CorePreset.ToNumber(args[1]);
            if (size <= 0) throw new EvaluationException("chunk size must be positive");
            var outer = ImmutableArray.CreateBuilder<Value>();
            for (var i = 0; i < a.Items.Length; i += size)
            {
                var end = Math.Min(i + size, a.Items.Length);
                outer.Add(new Value.Array([.. a.Items.AsSpan(i, end - i).ToArray()]));
            }
            return new Value.Array(outer.ToImmutable());
        }));

        b.AddFunction("union", OperatorTableBuilder.Sync(args =>
        {
            var result = new List<Value>();
            var seen = new List<Value>();
            foreach (var a in args)
            {
                if (a is not Value.Array arr) continue;
                foreach (var item in arr.Items)
                {
                    var dup = seen.Any(s => CorePreset.StrictEquals(s, item));
                    if (!dup)
                    {
                        result.Add(item);
                        seen.Add(item);
                    }
                }
            }
            return new Value.Array([.. result]);
        }));

        b.AddFunction("intersect", OperatorTableBuilder.Sync(args =>
        {
            if (args.Length == 0 || args[0] is not Value.Array first) return Value.Undefined.Instance;
            var result = new List<Value>();
            foreach (var item in first.Items)
            {
                if (result.Any(r => CorePreset.StrictEquals(r, item))) continue;
                var keep = true;
                for (var i = 1; i < args.Length; i++)
                {
                    if (args[i] is not Value.Array other || !other.Items.Any(x => CorePreset.StrictEquals(x, item)))
                    {
                        keep = false;
                        break;
                    }
                }
                if (keep) result.Add(item);
            }
            return new Value.Array([.. result]);
        }));

        b.AddFunction("flatten", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Array a) return Value.Undefined.Instance;
            var depth = args.Length >= 2 && args[1] is Value.Number dn ? (int)dn.V : 1;
            var result = new List<Value>();
            FlattenInto(a, depth, result);
            return new Value.Array([.. result]);
        }));
    }

    // ---------- helpers ----------

    private static void HigherOrder(
        OperatorTableBuilder b,
        string name,
        Func<ImmutableArray<Value>, Value.Function, EvalContext, ValueTask<Value>> impl)
    {
        b.AddFunction(name, async (args, ctx) =>
        {
            var (arr, fn) = ResolveArrayAndFunction(args, name);
            return await impl(arr.Items, fn, ctx).ConfigureAwait(false);
        });
    }

    private static void HigherOrderWithInit(
        OperatorTableBuilder b,
        string name,
        Func<ImmutableArray<Value>, Value, Value.Function, EvalContext, ValueTask<Value>> impl)
    {
        b.AddFunction(name, async (args, ctx) =>
        {
            // Either (array, init, fn) or (fn, init, array).
            if (args.Length < 3) throw new ExpressionArgumentException($"{name} requires 3 arguments", name);
            Value.Array arr;
            Value init;
            Value.Function fn;
            if (args[0] is Value.Array a0 && args[2] is Value.Function f2)
            {
                arr = a0;
                init = args[1];
                fn = f2;
            }
            else if (args[0] is Value.Function f0 && args[2] is Value.Array a2)
            {
                fn = f0;
                init = args[1];
                arr = a2;
            }
            else
            {
                throw new ExpressionArgumentException($"{name} expects (array, init, fn) or (fn, init, array)", name);
            }
            return await impl(arr.Items, init, fn, ctx).ConfigureAwait(false);
        });
    }

    private static (Value.Array Arr, Value.Function Fn) ResolveArrayAndFunction(Value[] args, string fnName)
    {
        if (args.Length < 2) throw new ExpressionArgumentException($"{fnName} requires 2 arguments", fnName);
        if (args[0] is Value.Array a0 && args[1] is Value.Function f1) return (a0, f1);
        if (args[0] is Value.Function f0 && args[1] is Value.Array a1) return (a1, f0);
        throw new ExpressionArgumentException($"{fnName} expects (array, fn) or (fn, array)", fnName);
    }

    private static async ValueTask<Value> SortAsync(Value[] args, EvalContext ctx)
    {
        Value.Array arr;
        Value.Function? cmp = null;
        if (args.Length == 1 && args[0] is Value.Array a1)
        {
            arr = a1;
        }
        else if (args.Length >= 2 && args[0] is Value.Array a2 && args[1] is Value.Function f2)
        {
            arr = a2;
            cmp = f2;
        }
        else if (args.Length >= 2 && args[0] is Value.Function f0 && args[1] is Value.Array a0)
        {
            arr = a0;
            cmp = f0;
        }
        else
        {
            return Value.Undefined.Instance;
        }

        var items = arr.Items.ToArray();
        if (cmp is null)
        {
            Array.Sort(items, (x, y) => DefaultCompare(x, y));
        }
        else
        {
            // Can't use Array.Sort with async comparator — eagerly materialise
            // pairwise comparisons into a map, then sort with lookup.
            var results = new Dictionary<(int, int), int>();
            for (var i = 0; i < items.Length; i++)
            {
                for (var j = i + 1; j < items.Length; j++)
                {
                    var r = await cmp.Invoke([items[i], items[j]], ctx);
                    var n = (int)CorePreset.ToNumber(r);
                    results[(i, j)] = n;
                    results[(j, i)] = -n;
                }
            }
            var indices = Enumerable.Range(0, items.Length).ToArray();
            Array.Sort(indices, (i, j) => i == j ? 0 : results[(i, j)]);
            items = indices.Select(i => items[i]).ToArray();
        }
        return new Value.Array([.. items]);
    }

    private static int DefaultCompare(Value a, Value b)
    {
        return (a, b) switch
        {
            (Value.Number na, Value.Number nb) => na.V.CompareTo(nb.V),
            (Value.String sa, Value.String sb) => string.CompareOrdinal(sa.V, sb.V),
            _ => string.CompareOrdinal(CorePreset.ToStringValue(a), CorePreset.ToStringValue(b)),
        };
    }

    private static Value Dedupe(Value[] args)
    {
        if (args[0] is not Value.Array a) return Value.Undefined.Instance;
        var result = new List<Value>();
        foreach (var item in a.Items)
        {
            if (!result.Any(r => CorePreset.StrictEquals(r, item)))
            {
                result.Add(item);
            }
        }
        return new Value.Array([.. result]);
    }

    private static void FlattenInto(Value.Array a, int depth, List<Value> dest)
    {
        foreach (var item in a.Items)
        {
            if (item is Value.Array inner && depth > 0)
            {
                FlattenInto(inner, depth - 1, dest);
            }
            else
            {
                dest.Add(item);
            }
        }
    }
}
