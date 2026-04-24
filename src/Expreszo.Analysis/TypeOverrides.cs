using System.Collections.Immutable;

namespace Expreszo.Analysis;

/// <summary>
/// Selective type annotations overlaid on the plain <see cref="BuiltinMetadata"/>
/// catalogue. Only entries listed here participate in type validation — the
/// rest stay at permissive defaults (variadic, unknown return).
/// </summary>
/// <remarks>
/// Deliberately not exhaustive. Coverage matches the validator's payoff: the
/// top-arity-mismatch and operand-shape diagnostics rely on these; the
/// "don't fire on anything dynamic" stance relies on everything else
/// remaining <see cref="ValueKind.Unknown"/>.
/// </remarks>
internal readonly record struct TypeOverride(
    ValueKind ReturnKind,
    int MinArity,
    int MaxArity,
    ImmutableArray<ValueKind> ParameterKinds
);

internal static class TypeOverrides
{
    public static ImmutableDictionary<string, TypeOverride> All { get; } = Build();

    private static ImmutableDictionary<string, TypeOverride> Build()
    {
        var b = ImmutableDictionary.CreateBuilder<string, TypeOverride>(StringComparer.Ordinal);

        // ---- math unary: Number → Number ----
        string[] mathUnary =
        [
            "abs", "ceil", "floor", "round", "sign", "sqrt", "cbrt", "trunc",
            "exp", "expm1", "log", "ln", "log1p", "log2", "log10", "lg",
            "sin", "cos", "tan", "asin", "acos", "atan",
            "sinh", "cosh", "tanh", "asinh", "acosh", "atanh",
        ];
        foreach (string name in mathUnary)
        {
            b[name] = new TypeOverride(ValueKind.Number, 1, 1, [ValueKind.Number]);
        }

        // ---- length: anything-ish → Number ----
        b["length"] = new TypeOverride(ValueKind.Number, 1, 1, [ValueKind.Unknown]);

        // ---- math functions with precise shapes ----
        b["atan2"] = new(ValueKind.Number, 2, 2, [ValueKind.Number, ValueKind.Number]);
        b["clamp"] = new(ValueKind.Number, 3, 3, [ValueKind.Number, ValueKind.Number, ValueKind.Number]);
        b["fac"] = new(ValueKind.Number, 1, 1, [ValueKind.Number]);
        b["gamma"] = new(ValueKind.Number, 1, 1, [ValueKind.Number]);
        b["hypot"] = new(ValueKind.Number, 1, int.MaxValue, []);
        b["max"] = new(ValueKind.Number, 1, int.MaxValue, []);
        b["min"] = new(ValueKind.Number, 1, int.MaxValue, []);
        b["pow"] = new(ValueKind.Number, 2, 2, [ValueKind.Number, ValueKind.Number]);
        b["random"] = new(ValueKind.Number, 0, 2, []);
        b["roundTo"] = new(ValueKind.Number, 2, 2, [ValueKind.Number, ValueKind.Number]);
        b["sum"] = new(ValueKind.Number, 1, 1, [ValueKind.Array]);
        b["mean"] = new(ValueKind.Number, 1, 1, [ValueKind.Array]);
        b["median"] = new(ValueKind.Number, 1, 1, [ValueKind.Array]);
        b["mostFrequent"] = new(ValueKind.Unknown, 1, 1, [ValueKind.Array]);
        b["variance"] = new(ValueKind.Number, 1, 1, [ValueKind.Array]);
        b["stddev"] = new(ValueKind.Number, 1, 1, [ValueKind.Array]);
        b["percentile"] = new(ValueKind.Number, 2, 2, [ValueKind.Array, ValueKind.Number]);

        // ---- array functions ----
        b["count"] = new(ValueKind.Number, 1, 1, [ValueKind.Array]);
        b["filter"] = new(ValueKind.Array, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["fold"] = new(ValueKind.Unknown, 3, 3, [ValueKind.Array, ValueKind.Unknown, ValueKind.Function]);
        b["reduce"] = new(ValueKind.Unknown, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["find"] = new(ValueKind.Unknown, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["some"] = new(ValueKind.Boolean, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["every"] = new(ValueKind.Boolean, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["unique"] = new(ValueKind.Array, 1, 1, [ValueKind.Array]);
        b["distinct"] = new(ValueKind.Array, 1, 1, [ValueKind.Array]);
        b["indexOf"] = new(ValueKind.Number, 2, 2, [ValueKind.Array, ValueKind.Unknown]);
        b["join"] = new(ValueKind.String, 2, 2, [ValueKind.Array, ValueKind.String]);
        b["map"] = new(ValueKind.Array, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["range"] = new(ValueKind.Array, 2, 3, [ValueKind.Number, ValueKind.Number, ValueKind.Number]);
        b["chunk"] = new(ValueKind.Array, 2, 2, [ValueKind.Array, ValueKind.Number]);
        b["union"] = new(ValueKind.Array, 2, 2, [ValueKind.Array, ValueKind.Array]);
        b["intersect"] = new(ValueKind.Array, 2, 2, [ValueKind.Array, ValueKind.Array]);
        b["groupBy"] = new(ValueKind.Object, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["countBy"] = new(ValueKind.Object, 2, 2, [ValueKind.Array, ValueKind.Function]);
        b["sort"] = new(ValueKind.Array, 1, 2, [ValueKind.Array, ValueKind.Function]);
        b["flatten"] = new(ValueKind.Array, 1, 2, [ValueKind.Array, ValueKind.Number]);

        // ---- string functions ----
        b["isEmpty"] = new(ValueKind.Boolean, 1, 1, [ValueKind.String]);
        b["contains"] = new(ValueKind.Boolean, 2, 2, [ValueKind.String, ValueKind.String]);
        b["startsWith"] = new(ValueKind.Boolean, 2, 2, [ValueKind.String, ValueKind.String]);
        b["endsWith"] = new(ValueKind.Boolean, 2, 2, [ValueKind.String, ValueKind.String]);
        b["searchCount"] = new(ValueKind.Number, 2, 2, [ValueKind.String, ValueKind.String]);
        b["trim"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["toUpper"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["toLower"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["toTitle"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["split"] = new(ValueKind.Array, 2, 2, [ValueKind.String, ValueKind.String]);
        b["repeat"] = new(ValueKind.String, 2, 2, [ValueKind.String, ValueKind.Number]);
        b["reverse"] = new(ValueKind.Unknown, 1, 1, []);
        b["left"] = new(ValueKind.String, 2, 2, [ValueKind.String, ValueKind.Number]);
        b["right"] = new(ValueKind.String, 2, 2, [ValueKind.String, ValueKind.Number]);
        b["replace"] = new(ValueKind.String, 3, 3, [ValueKind.String, ValueKind.String, ValueKind.String]);
        b["replaceFirst"] = new(ValueKind.String, 3, 3, [ValueKind.String, ValueKind.String, ValueKind.String]);
        b["naturalSort"] = new(ValueKind.Array, 1, 1, [ValueKind.Array]);
        b["toNumber"] = new(ValueKind.Number, 1, 1, [ValueKind.String]);
        b["toBoolean"] = new(ValueKind.Boolean, 1, 1, [ValueKind.String]);
        b["padLeft"] = new(ValueKind.String, 2, 3, [ValueKind.String, ValueKind.Number, ValueKind.String]);
        b["padRight"] = new(ValueKind.String, 2, 3, [ValueKind.String, ValueKind.Number, ValueKind.String]);
        b["padBoth"] = new(ValueKind.String, 2, 3, [ValueKind.String, ValueKind.Number, ValueKind.String]);
        b["slice"] = new(ValueKind.String, 2, 3, [ValueKind.String, ValueKind.Number, ValueKind.Number]);
        b["urlEncode"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["base64Encode"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["base64Decode"] = new(ValueKind.String, 1, 1, [ValueKind.String]);
        b["coalesce"] = new(ValueKind.Unknown, 1, int.MaxValue, []);

        // ---- object functions ----
        b["merge"] = new(ValueKind.Object, 1, int.MaxValue, []);
        b["keys"] = new(ValueKind.Array, 1, 1, [ValueKind.Object]);
        b["values"] = new(ValueKind.Array, 1, 1, [ValueKind.Object]);
        b["mapValues"] = new(ValueKind.Object, 2, 2, [ValueKind.Object, ValueKind.Function]);
        b["pick"] = new(ValueKind.Object, 2, 2, [ValueKind.Object, ValueKind.Array]);
        b["omit"] = new(ValueKind.Object, 2, 2, [ValueKind.Object, ValueKind.Array]);
        b["flattenObject"] = new(ValueKind.Object, 1, 2, [ValueKind.Object, ValueKind.String]);

        // ---- utility ----
        b["if"] = new(ValueKind.Unknown, 3, 3, []);
        b["json"] = new(ValueKind.String, 1, 1, []);

        // ---- type checks: anything → Boolean ----
        string[] typeChecks =
        [
            "isArray", "isObject", "isNumber", "isString",
            "isBoolean", "isNull", "isUndefined", "isFunction",
        ];
        foreach (string name in typeChecks)
        {
            b[name] = new TypeOverride(ValueKind.Boolean, 1, 1, [ValueKind.Unknown]);
        }

        return b.ToImmutable();
    }
}
