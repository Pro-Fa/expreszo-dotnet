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
        b.AddFunction(
            "count",
            OperatorTableBuilder.Sync(args =>
                args[0] is Value.Array a
                    ? Value.Number.Of(a.Items.Length)
                    : Value.Undefined.Instance
            )
        );

        HigherOrder(
            b,
            "filter",
            async (arr, fn, ctx) =>
            {
                ImmutableArray<Value>.Builder result = ImmutableArray.CreateBuilder<Value>();

                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    Value ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                    if (ok.IsTruthy())
                    {
                        result.Add(arr[i]);
                    }
                }
                return new Value.Array(result.ToImmutable());
            }
        );

        HigherOrderWithInit(
            b,
            "fold",
            async (arr, init, fn, ctx) =>
            {
                Value acc = init;
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    acc = await fn.Invoke([acc, arr[i], Value.Number.Of(i)], ctx);
                }
                return acc;
            }
        );

        HigherOrderWithInit(
            b,
            "reduce",
            async (arr, init, fn, ctx) =>
            {
                Value acc = init;
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    acc = await fn.Invoke([acc, arr[i], Value.Number.Of(i)], ctx);
                }
                return acc;
            }
        );

        HigherOrder(
            b,
            "find",
            async (arr, fn, ctx) =>
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    Value ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                    if (ok.IsTruthy())
                    {
                        return arr[i];
                    }
                }
                return Value.Undefined.Instance;
            }
        );

        HigherOrder(
            b,
            "some",
            async (arr, fn, ctx) =>
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    Value ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                    if (ok.IsTruthy())
                    {
                        return Value.Boolean.True;
                    }
                }
                return Value.Boolean.False;
            }
        );

        HigherOrder(
            b,
            "every",
            async (arr, fn, ctx) =>
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    Value ok = await fn.Invoke([arr[i], Value.Number.Of(i)], ctx);
                    if (!ok.IsTruthy())
                    {
                        return Value.Boolean.False;
                    }
                }
                return Value.Boolean.True;
            }
        );

        HigherOrder(
            b,
            "map",
            async (arr, fn, ctx) =>
            {
                ImmutableArray<Value>.Builder result = ImmutableArray.CreateBuilder<Value>(
                    arr.Length
                );
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    result.Add(await fn.Invoke([arr[i], Value.Number.Of(i)], ctx));
                }
                return new Value.Array(result.ToImmutable());
            }
        );

        HigherOrder(
            b,
            "groupBy",
            async (arr, fn, ctx) =>
            {
                var dict = new Dictionary<string, List<Value>>(StringComparer.Ordinal);
                var order = new List<string>();
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    string key = CorePreset.ToStringValue(
                        await fn.Invoke([arr[i], Value.Number.Of(i)], ctx)
                    );
                    if (!dict.TryGetValue(key, out List<Value>? bucket))
                    {
                        bucket = new List<Value>();
                        dict[key] = bucket;
                        order.Add(key);
                    }
                    bucket.Add(arr[i]);
                }
                var result = new Dictionary<string, Value>(StringComparer.Ordinal);
                foreach (string k in order)
                {
                    result[k] = new Value.Array([.. dict[k]]);
                }

                return Value.Object.From(result);
            }
        );

        HigherOrder(
            b,
            "countBy",
            async (arr, fn, ctx) =>
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                var order = new List<string>();
                for (int i = 0; i < arr.Length; i++)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    string key = CorePreset.ToStringValue(
                        await fn.Invoke([arr[i], Value.Number.Of(i)], ctx)
                    );
                    if (!counts.TryGetValue(key, out int c))
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
                foreach (string k in order)
                {
                    result[k] = Value.Number.Of(counts[k]);
                }

                return Value.Object.From(result);
            }
        );

        b.AddFunction("sort", (args, ctx) => SortAsync(args, ctx));

        b.AddFunction("unique", OperatorTableBuilder.Sync(args => Dedupe(args)));
        b.AddFunction("distinct", OperatorTableBuilder.Sync(args => Dedupe(args)));

        b.AddFunction(
            "indexOf",
            OperatorTableBuilder.Sync(args =>
            {
                (Value haystack, Value needle) = (args[0], args[1]);
                if (haystack is Value.Array a)
                {
                    for (int i = 0; i < a.Items.Length; i++)
                    {
                        if (CorePreset.StrictEquals(a.Items[i], needle))
                        {
                            return Value.Number.Of(i);
                        }
                    }
                    return Value.Number.Of(-1);
                }
                if (haystack is Value.String s)
                {
                    if (needle is Value.String n)
                    {
                        return Value.Number.Of(s.V.IndexOf(n.V, StringComparison.Ordinal));
                    }
                }
                return Value.Number.Of(-1);
            })
        );

        b.AddFunction(
            "join",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.Array a || args[1] is not Value.String sep)
                {
                    return Value.Undefined.Instance;
                }

                return new Value.String(
                    string.Join(sep.V, a.Items.Select(CorePreset.ToStringValue))
                );
            })
        );

        b.AddFunction(
            "range",
            OperatorTableBuilder.Sync(args =>
            {
                double start = CorePreset.ToNumber(args[0]);
                double end = CorePreset.ToNumber(args[1]);
                double step =
                    args.Length >= 3 && args[2] is not Value.Undefined
                        ? CorePreset.ToNumber(args[2])
                        : 1;
                if (step == 0)
                {
                    throw new EvaluationException("range step cannot be zero");
                }

                if (
                    double.IsNaN(start)
                    || double.IsNaN(end)
                    || double.IsNaN(step)
                    || double.IsInfinity(start)
                    || double.IsInfinity(end)
                    || double.IsInfinity(step)
                )
                {
                    throw new EvaluationException("range arguments must be finite");
                }

                double span = Math.Abs(end - start) / Math.Abs(step);
                if (span > EvaluationLimits.MaxArrayLength)
                {
                    throw new EvaluationException(
                        $"range would produce more than {EvaluationLimits.MaxArrayLength} elements"
                    );
                }

                int capacity = span > int.MaxValue ? 0 : (int)Math.Ceiling(span);
                ImmutableArray<Value>.Builder result = ImmutableArray.CreateBuilder<Value>(
                    capacity
                );
                if (step > 0)
                {
                    for (double x = start; x < end; x += step)
                    {
                        result.Add(Value.Number.Of(x));
                    }
                }
                else
                {
                    for (double x = start; x > end; x += step)
                    {
                        result.Add(Value.Number.Of(x));
                    }
                }
                return new Value.Array(result.ToImmutable());
            })
        );

        b.AddFunction(
            "chunk",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.Array a)
                {
                    return Value.Undefined.Instance;
                }

                int size = (int)CorePreset.ToNumber(args[1]);
                if (size <= 0)
                {
                    throw new EvaluationException("chunk size must be positive");
                }

                ImmutableArray<Value>.Builder outer = ImmutableArray.CreateBuilder<Value>();
                for (int i = 0; i < a.Items.Length; i += size)
                {
                    int end = Math.Min(i + size, a.Items.Length);
                    outer.Add(new Value.Array([.. a.Items.AsSpan(i, end - i).ToArray()]));
                }
                return new Value.Array(outer.ToImmutable());
            })
        );

        b.AddFunction(
            "union",
            OperatorTableBuilder.Sync(args =>
            {
                var result = new List<Value>();
                var seen = new List<Value>();
                foreach (Value a in args)
                {
                    if (a is not Value.Array arr)
                    {
                        continue;
                    }

                    foreach (Value item in arr.Items)
                    {
                        bool dup = seen.Any(s => CorePreset.StrictEquals(s, item));
                        if (!dup)
                        {
                            result.Add(item);
                            seen.Add(item);
                        }
                    }
                }
                return new Value.Array([.. result]);
            })
        );

        b.AddFunction(
            "intersect",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length == 0 || args[0] is not Value.Array first)
                {
                    return Value.Undefined.Instance;
                }

                var result = new List<Value>();
                foreach (Value item in first.Items)
                {
                    if (result.Any(r => CorePreset.StrictEquals(r, item)))
                    {
                        continue;
                    }

                    bool keep = true;
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (
                            args[i] is not Value.Array other
                            || !other.Items.Any(x => CorePreset.StrictEquals(x, item))
                        )
                        {
                            keep = false;
                            break;
                        }
                    }
                    if (keep)
                    {
                        result.Add(item);
                    }
                }
                return new Value.Array([.. result]);
            })
        );

        b.AddFunction(
            "flatten",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.Array a)
                {
                    return Value.Undefined.Instance;
                }

                int depth = args.Length >= 2 && args[1] is Value.Number dn ? (int)dn.V : 1;
                var result = new List<Value>();
                FlattenInto(a, depth, result);
                return new Value.Array([.. result]);
            })
        );
    }

    // ---------- helpers ----------

    private static void HigherOrder(
        OperatorTableBuilder b,
        string name,
        Func<ImmutableArray<Value>, Value.Function, EvalContext, ValueTask<Value>> impl
    )
    {
        b.AddFunction(
            name,
            async (args, ctx) =>
            {
                (Value.Array arr, Value.Function fn) = ResolveArrayAndFunction(args, name);
                return await impl(arr.Items, fn, ctx).ConfigureAwait(false);
            }
        );
    }

    private static void HigherOrderWithInit(
        OperatorTableBuilder b,
        string name,
        Func<ImmutableArray<Value>, Value, Value.Function, EvalContext, ValueTask<Value>> impl
    )
    {
        b.AddFunction(
            name,
            async (args, ctx) =>
            {
                if (args.Length < 3)
                {
                    throw new ExpressionArgumentException($"{name} requires 3 arguments", name);
                }

                (Value.Array arr, Value.Function fn) =
                    TryResolveArrayFunctionPair(args[0], args[2])
                    ?? throw new ExpressionArgumentException(
                        $"{name} expects (array, init, fn) or (fn, init, array)",
                        name
                    );

                return await impl(arr.Items, args[1], fn, ctx).ConfigureAwait(false);
            }
        );
    }

    /// <summary>
    /// Accepts either (array, function) or (function, array) and returns them
    /// canonically; returns null when neither shape matches. Used by the 2-arg
    /// HOFs, the (a, init, f) / (f, init, a) 3-arg HOFs, and sort's optional
    /// comparator shape.
    /// </summary>
    private static (Value.Array Arr, Value.Function Fn)? TryResolveArrayFunctionPair(
        Value left,
        Value right
    )
    {
        if (left is Value.Array la && right is Value.Function rf)
        {
            return (la, rf);
        }
        if (left is Value.Function lf && right is Value.Array ra)
        {
            return (ra, lf);
        }
        return null;
    }

    private static (Value.Array Arr, Value.Function Fn) ResolveArrayAndFunction(
        Value[] args,
        string fnName
    )
    {
        if (args.Length < 2)
        {
            throw new ExpressionArgumentException($"{fnName} requires 2 arguments", fnName);
        }

        return TryResolveArrayFunctionPair(args[0], args[1])
            ?? throw new ExpressionArgumentException(
                $"{fnName} expects (array, fn) or (fn, array)",
                fnName
            );
    }

    private static async ValueTask<Value> SortAsync(Value[] args, EvalContext ctx)
    {
        (Value.Array? arr, Value.Function? cmp) = ResolveSortArguments(args);
        if (arr is null)
        {
            return Value.Undefined.Instance;
        }

        Value[] items = arr.Items.ToArray();
        if (cmp is null)
        {
            Array.Sort(items, (x, y) => DefaultCompare(x, y));
        }
        else
        {
            // Merge sort - O(n log n) comparator calls, stable, and awaits
            // each comparison on demand so sync comparators never allocate
            // the async state machine.
            var buffer = new Value[items.Length];
            await MergeSortAsync(items, buffer, 0, items.Length, cmp, ctx).ConfigureAwait(false);
        }

        return new Value.Array([.. items]);
    }

    private static (Value.Array? Arr, Value.Function? Cmp) ResolveSortArguments(Value[] args)
    {
        if (args.Length == 1 && args[0] is Value.Array solo)
        {
            return (solo, null);
        }
        if (args.Length >= 2 && TryResolveArrayFunctionPair(args[0], args[1]) is { } pair)
        {
            return (pair.Arr, pair.Fn);
        }
        return (null, null);
    }

    private static async ValueTask MergeSortAsync(
        Value[] items,
        Value[] buffer,
        int lo,
        int hi,
        Value.Function cmp,
        EvalContext ctx
    )
    {
        if (hi - lo < 2)
        {
            return;
        }

        ctx.CancellationToken.ThrowIfCancellationRequested();
        int mid = lo + ((hi - lo) / 2);
        await MergeSortAsync(items, buffer, lo, mid, cmp, ctx).ConfigureAwait(false);
        await MergeSortAsync(items, buffer, mid, hi, cmp, ctx).ConfigureAwait(false);

        int i = lo,
            j = mid,
            k = lo;
        while (i < mid && j < hi)
        {
            Value r = await cmp.Invoke([items[i], items[j]], ctx).ConfigureAwait(false);
            if (CorePreset.ToNumber(r) <= 0)
            {
                buffer[k++] = items[i++];
            }
            else
            {
                buffer[k++] = items[j++];
            }
        }
        while (i < mid)
        {
            buffer[k++] = items[i++];
        }

        while (j < hi)
        {
            buffer[k++] = items[j++];
        }

        Array.Copy(buffer, lo, items, lo, hi - lo);
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
        if (args[0] is not Value.Array a)
        {
            return Value.Undefined.Instance;
        }

        var result = new List<Value>();

        foreach (Value item in a.Items)
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
        foreach (Value item in a.Items)
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
