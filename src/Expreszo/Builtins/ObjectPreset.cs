using System.Collections.Immutable;
using Expreszo.Errors;

namespace Expreszo.Builtins;

internal static class ObjectPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        b.AddFunction("merge", OperatorTableBuilder.Sync(args =>
        {
            var dict = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var a in args)
            {
                if (a is Value.Object o)
                {
                    foreach (var kv in o.Props)
                    {
                        dict[kv.Key] = kv.Value;
                    }
                }
            }
            return Value.Object.From(dict);
        }));

        b.AddFunction("keys", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Object o) return Value.Undefined.Instance;
            return new Value.Array([.. o.Props.Keys.Select(k => (Value)new Value.String(k))]);
        }));

        b.AddFunction("values", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Object o) return Value.Undefined.Instance;
            return new Value.Array([.. o.Props.Values]);
        }));

        b.AddFunction("mapValues", async (args, ctx) =>
        {
            if (args[0] is not Value.Object o || args[1] is not Value.Function fn) return Value.Undefined.Instance;
            var result = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var kv in o.Props)
            {
                result[kv.Key] = await fn.Invoke([kv.Value, new Value.String(kv.Key)], ctx);
            }
            return Value.Object.From(result);
        });

        b.AddFunction("pick", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Object o) return Value.Undefined.Instance;
            var keys = ExtractKeys(args[1]);
            var result = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                if (o.Props.TryGetValue(k, out var v)) result[k] = v;
            }
            return Value.Object.From(result);
        }));

        b.AddFunction("omit", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Object o) return Value.Undefined.Instance;
            var keys = new HashSet<string>(ExtractKeys(args[1]), StringComparer.Ordinal);
            var result = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var kv in o.Props)
            {
                if (!keys.Contains(kv.Key)) result[kv.Key] = kv.Value;
            }
            return Value.Object.From(result);
        }));

        // Note: the original library overloads `flatten` for arrays AND
        // objects. The array flatten is registered in ArrayPreset; this is
        // the object variant, which flattens nested objects into a single
        // level with separator-joined keys.
        b.AddFunction("flattenObject", OperatorTableBuilder.Sync(args =>
        {
            if (args[0] is not Value.Object o) return Value.Undefined.Instance;
            var sep = args.Length >= 2 && args[1] is Value.String s ? s.V : "_";
            var result = new Dictionary<string, Value>(StringComparer.Ordinal);
            FlattenObject(o, "", sep, result);
            return Value.Object.From(result);
        }));
    }

    private static IEnumerable<string> ExtractKeys(Value v) => v switch
    {
        Value.String s => new[] { s.V },
        Value.Array a => a.Items.OfType<Value.String>().Select(s => s.V),
        _ => Array.Empty<string>(),
    };

    private static void FlattenObject(Value.Object o, string prefix, string sep, Dictionary<string, Value> result)
    {
        foreach (var kv in o.Props)
        {
            var key = prefix.Length == 0 ? kv.Key : prefix + sep + kv.Key;
            if (kv.Value is Value.Object inner)
            {
                FlattenObject(inner, key, sep, result);
            }
            else
            {
                result[key] = kv.Value;
            }
        }
    }
}
