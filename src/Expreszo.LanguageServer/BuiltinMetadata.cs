using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Expreszo.LanguageServer;

/// <summary>
/// Static catalogue of ExpresZo operators, built-in functions, and keywords
/// keyed by name. Used by hover, completion, and semantic-highlight
/// classification. Signatures are short-form documentation strings, not
/// parser-consumed schemas.
/// </summary>
internal static class BuiltinMetadata
{
    private static readonly BuiltinEntry[] All = BuildEntries();

    private static readonly FrozenDictionary<string, BuiltinEntry> ByName =
        All.ToFrozenDictionary(e => e.Name, StringComparer.Ordinal);

    /// <summary>All catalogued entries, stable order (operators → keywords → functions).</summary>
    public static IReadOnlyList<BuiltinEntry> Entries => All;

    /// <summary>Tries to find an entry by its canonical name.</summary>
    public static bool TryGet(string name, out BuiltinEntry entry)
    {
        if (ByName.TryGetValue(name, out BuiltinEntry? value))
        {
            entry = value;
            return true;
        }

        entry = default!;
        return false;
    }

    /// <summary>Returns true if <paramref name="name"/> is a known built-in function.</summary>
    public static bool IsBuiltinFunction(string name) =>
        ByName.TryGetValue(name, out BuiltinEntry? entry) && entry.Kind == BuiltinKind.Function;

    /// <summary>Returns true if <paramref name="name"/> is a reserved keyword.</summary>
    public static bool IsKeyword(string name) =>
        ByName.TryGetValue(name, out BuiltinEntry? entry) && entry.Kind == BuiltinKind.Keyword;

    private static BuiltinEntry[] BuildEntries()
    {
        List<BuiltinEntry> list =
        [
            // --- keywords / constants -------------------------------------
            Kw("case", "case x when … then … else … end", "Pattern-style conditional."),
            Kw("when", "case subject when pattern then result …", "Pattern head inside a case."),
            Kw("then", "case … when … then result", "Result head inside a case arm."),
            Kw("else", "case … else fallback end", "Fallback arm inside a case."),
            Kw("end", "case … end", "Terminator for a case expression."),
            Kw("true", "true", "Boolean literal."),
            Kw("false", "false", "Boolean literal."),
            Kw("null", "null", "Explicit null value (distinct from undefined)."),
            Kw("undefined", "undefined", "Missing-value marker (distinct from null)."),

            // --- binary operators ----------------------------------------
            Op("+", "a + b", "Numeric addition; both sides must be numbers."),
            Op("-", "a - b", "Numeric subtraction (also used as unary negation)."),
            Op("*", "a * b", "Numeric multiplication."),
            Op("/", "a / b", "Numeric division; throws on divide-by-zero."),
            Op("%", "a % b", "Remainder."),
            Op("^", "a ^ b", "Exponentiation (base ^ exponent)."),
            Op("|", "a | b", "Concatenation. Arrays: element-wise concat. Otherwise: stringify-and-join."),
            Op("==", "a == b", "Strict equality (NaN != NaN; arrays/objects compared by reference)."),
            Op("!=", "a != b", "Strict inequality."),
            Op("<", "a < b", "Less-than. Ordinal compare for strings, numeric otherwise."),
            Op("<=", "a <= b", "Less-than-or-equal."),
            Op(">", "a > b", "Greater-than."),
            Op(">=", "a >= b", "Greater-than-or-equal."),
            Op("in", "needle in array", "True when `needle` strict-equals any element of `array`."),
            Op("not in", "needle not in array", "Negation of the `in` operator."),
            Op("and", "a and b", "Logical AND (short-circuits). Alias: &&."),
            Op("&&", "a && b", "Logical AND (short-circuits). Alias: and."),
            Op("or", "a or b", "Logical OR (short-circuits). Alias: ||."),
            Op("||", "a || b", "Logical OR (short-circuits). Alias: or."),
            Op("??", "a ?? fallback", "Null-coalesce. Treats null, undefined, NaN, and ±Infinity as nullish."),
            Op("as", "value as \"number\"", "Type cast. Targets: \"number\", \"int\" / \"integer\", \"boolean\"."),
            Op("?:", "cond ? a : b", "Ternary conditional."),
            Op("not", "not x", "Logical negation. Alias: prefix `!`."),
            Op("!", "x!", "Postfix factorial (prefix `!` means `not`)."),

            // --- math unary ----------------------------------------------
            Fn("abs", "abs(x)", "Absolute value."),
            Fn("ceil", "ceil(x)", "Round up to the nearest integer."),
            Fn("floor", "floor(x)", "Round down to the nearest integer."),
            Fn("round", "round(x)", "Round half away from zero."),
            Fn("sign", "sign(x)", "Sign of x (-1, 0, or 1)."),
            Fn("sqrt", "sqrt(x)", "Square root."),
            Fn("cbrt", "cbrt(x)", "Cube root."),
            Fn("trunc", "trunc(x)", "Truncate toward zero."),
            Fn("exp", "exp(x)", "e^x."),
            Fn("expm1", "expm1(x)", "e^x - 1, accurate for small x."),
            Fn("log", "log(x)", "Natural logarithm. Alias: ln."),
            Fn("ln", "ln(x)", "Natural logarithm. Alias: log."),
            Fn("log1p", "log1p(x)", "log(1 + x), accurate for small x."),
            Fn("log2", "log2(x)", "Base-2 logarithm."),
            Fn("log10", "log10(x)", "Base-10 logarithm. Alias: lg."),
            Fn("lg", "lg(x)", "Base-10 logarithm. Alias: log10."),
            Fn("sin", "sin(x)", "Sine (radians)."),
            Fn("cos", "cos(x)", "Cosine (radians)."),
            Fn("tan", "tan(x)", "Tangent (radians)."),
            Fn("asin", "asin(x)", "Arcsine (radians)."),
            Fn("acos", "acos(x)", "Arccosine (radians)."),
            Fn("atan", "atan(x)", "Arctangent (radians)."),
            Fn("sinh", "sinh(x)", "Hyperbolic sine."),
            Fn("cosh", "cosh(x)", "Hyperbolic cosine."),
            Fn("tanh", "tanh(x)", "Hyperbolic tangent."),
            Fn("asinh", "asinh(x)", "Inverse hyperbolic sine."),
            Fn("acosh", "acosh(x)", "Inverse hyperbolic cosine."),
            Fn("atanh", "atanh(x)", "Inverse hyperbolic tangent."),
            Fn("length", "length(x)", "Length of a string, array, or stringified number."),

            // --- math functions ------------------------------------------
            Fn("atan2", "atan2(y, x)", "Two-argument arctangent."),
            Fn("clamp", "clamp(x, min, max)", "Constrain x to the [min, max] interval."),
            Fn("fac", "fac(n)", "Factorial. Prefix-form alias of postfix `!`."),
            Fn("gamma", "gamma(x)", "Gamma function (generalised factorial)."),
            Fn("hypot", "hypot(x, y, …)", "Euclidean norm √(Σ xᵢ²)."),
            Fn("max", "max(a, b, …) | max(array)", "Maximum across arguments or an array."),
            Fn("min", "min(a, b, …) | min(array)", "Minimum across arguments or an array."),
            Fn("pow", "pow(base, exp)", "Exponentiation (same as base ^ exp)."),
            Fn("random", "random() | random(max) | random(min, max)", "Uniform random double."),
            Fn("roundTo", "roundTo(x, digits)", "Round to N fractional digits."),
            Fn("sum", "sum(array)", "Sum of a numeric array."),
            Fn("mean", "mean(array)", "Arithmetic mean."),
            Fn("median", "median(array)", "Median value."),
            Fn("mostFrequent", "mostFrequent(array)", "Mode (most frequent element)."),
            Fn("variance", "variance(array)", "Population variance."),
            Fn("stddev", "stddev(array)", "Population standard deviation."),
            Fn("percentile", "percentile(array, p)", "Percentile at p (0..100)."),

            // --- array functions -----------------------------------------
            Fn("count", "count(array)", "Element count. Shorthand: length(array)."),
            Fn("filter", "filter(array, x => pred)", "Keep elements where pred is truthy."),
            Fn("fold", "fold(array, seed, (acc, x) => …)", "Left fold with an explicit seed."),
            Fn("reduce", "reduce(array, (acc, x) => …)", "Left fold seeded with the first element."),
            Fn("find", "find(array, x => pred)", "First element where pred is truthy, else undefined."),
            Fn("some", "some(array, x => pred)", "True if any element satisfies pred."),
            Fn("every", "every(array, x => pred)", "True if all elements satisfy pred."),
            Fn("unique", "unique(array)", "Deduplicate by strict equality, preserving order."),
            Fn("distinct", "distinct(array)", "Alias of unique."),
            Fn("indexOf", "indexOf(array, needle)", "Index of the first strict-equal match, or -1."),
            Fn("join", "join(array, separator)", "Stringify and join with `separator`."),
            Fn("map", "map(array, x => expr)", "Transform each element."),
            Fn("range", "range(start, stop) | range(start, stop, step)", "Inclusive-exclusive numeric range."),
            Fn("chunk", "chunk(array, size)", "Split into fixed-size chunks."),
            Fn("union", "union(a, b)", "Deduplicated concatenation."),
            Fn("intersect", "intersect(a, b)", "Elements present in both arrays."),
            Fn("groupBy", "groupBy(array, x => key)", "Group elements by key into an object of arrays."),
            Fn("countBy", "countBy(array, x => key)", "Count elements per key."),
            Fn("sort", "sort(array) | sort(array, x => key)", "Stable sort, ascending."),
            Fn("flatten", "flatten(array) | flatten(array, depth)", "Flatten nested arrays to depth (default 1)."),

            // --- string functions ----------------------------------------
            Fn("isEmpty", "isEmpty(string)", "True for empty or whitespace-only strings."),
            Fn("contains", "contains(haystack, needle)", "Substring check."),
            Fn("startsWith", "startsWith(s, prefix)", "Prefix check."),
            Fn("endsWith", "endsWith(s, suffix)", "Suffix check."),
            Fn("searchCount", "searchCount(s, needle)", "Number of non-overlapping occurrences."),
            Fn("trim", "trim(s)", "Strip leading and trailing whitespace."),
            Fn("toUpper", "toUpper(s)", "Upper-case."),
            Fn("toLower", "toLower(s)", "Lower-case."),
            Fn("toTitle", "toTitle(s)", "Title-case each word."),
            Fn("split", "split(s, separator)", "Split into an array."),
            Fn("repeat", "repeat(s, n)", "Repeat `s` n times (capped by EvaluationLimits)."),
            Fn("reverse", "reverse(s) | reverse(array)", "Reverse a string or array."),
            Fn("left", "left(s, n)", "First n characters."),
            Fn("right", "right(s, n)", "Last n characters."),
            Fn("replace", "replace(s, needle, replacement)", "Replace every occurrence."),
            Fn("replaceFirst", "replaceFirst(s, needle, replacement)", "Replace the first occurrence."),
            Fn("naturalSort", "naturalSort(array)", "Sort with natural-numeric ordering (file2 < file10)."),
            Fn("toNumber", "toNumber(s)", "Parse s as a number, or undefined if not parseable."),
            Fn("toBoolean", "toBoolean(s)", "Parse s as a boolean."),
            Fn("padLeft", "padLeft(s, width) | padLeft(s, width, pad)", "Left-pad to width."),
            Fn("padRight", "padRight(s, width) | padRight(s, width, pad)", "Right-pad to width."),
            Fn("padBoth", "padBoth(s, width) | padBoth(s, width, pad)", "Centre-pad to width."),
            Fn("slice", "slice(s, start) | slice(s, start, end)", "Substring by character offsets."),
            Fn("urlEncode", "urlEncode(s)", "Percent-encode for URL usage."),
            Fn("base64Encode", "base64Encode(s)", "Base64 encode."),
            Fn("base64Decode", "base64Decode(s)", "Base64 decode."),
            Fn("coalesce", "coalesce(a, b, …)", "First non-null, non-undefined argument."),

            // --- object functions ----------------------------------------
            Fn("merge", "merge(a, b, …)", "Shallow-merge objects; later keys win."),
            Fn("keys", "keys(object)", "Array of property names."),
            Fn("values", "values(object)", "Array of property values."),
            Fn("mapValues", "mapValues(object, v => expr)", "Transform each value; keys preserved."),
            Fn("pick", "pick(object, keys)", "Subset the object to the given key list."),
            Fn("omit", "omit(object, keys)", "Remove the given keys."),
            Fn("flattenObject", "flattenObject(object) | flattenObject(object, separator)",
                "Flatten nested object paths into a single level; separator defaults to \".\"."),

            // --- utility -------------------------------------------------
            Fn("if", "if(cond, thenExpr, elseExpr)", "Lazy conditional; only the chosen branch evaluates."),
            Fn("json", "json(value)", "Canonical JSON string representation."),

            // --- type checks ---------------------------------------------
            Fn("isArray", "isArray(x)", "True for arrays."),
            Fn("isObject", "isObject(x)", "True for objects."),
            Fn("isNumber", "isNumber(x)", "True for finite numbers."),
            Fn("isString", "isString(x)", "True for strings."),
            Fn("isBoolean", "isBoolean(x)", "True for booleans."),
            Fn("isNull", "isNull(x)", "True for explicit null only."),
            Fn("isUndefined", "isUndefined(x)", "True for undefined only."),
            Fn("isFunction", "isFunction(x)", "True for function values (lambda or defined)."),
        ];

        return [.. list];
    }

    private static BuiltinEntry Fn(string name, string signature, string summary) =>
        new(name, BuiltinKind.Function, signature, summary, ExtractParameters(signature));

    private static BuiltinEntry Op(string name, string signature, string summary) =>
        new(name, BuiltinKind.Operator, signature, summary, []);

    private static BuiltinEntry Kw(string name, string signature, string summary) =>
        new(name, BuiltinKind.Keyword, signature, summary, []);

    /// <summary>
    /// Best-effort extraction of parameter names from a signature string.
    /// Handles the common shapes we write: <c>name(a, b)</c>,
    /// <c>name(a, b, …)</c>, and <c>name(a) | name(array)</c> (picks the
    /// first form). Returns an empty array when the signature doesn't look
    /// like a simple call form.
    /// </summary>
    private static ImmutableArray<string> ExtractParameters(string signature)
    {
        int open = signature.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return [];
        }

        int close = signature.IndexOf(')', open + 1);
        if (close < 0)
        {
            return [];
        }

        string inner = signature.Substring(open + 1, close - open - 1).Trim();
        if (inner.Length == 0)
        {
            return [];
        }

        var result = ImmutableArray.CreateBuilder<string>();
        foreach (string raw in inner.Split(','))
        {
            string name = raw.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            result.Add(name);
        }

        return result.ToImmutable();
    }
}

/// <summary>Kind of a catalogued identifier.</summary>
internal enum BuiltinKind
{
    Function,
    Operator,
    Keyword,
}

/// <summary>One entry in the built-in catalogue.</summary>
internal sealed record BuiltinEntry(
    string Name,
    BuiltinKind Kind,
    string Signature,
    string Summary,
    ImmutableArray<string> Parameters
)
{
    /// <summary>Markdown-formatted hover content: code block with the signature plus a one-line summary.</summary>
    public string ToMarkdown() => $"```expreszo\n{Signature}\n```\n{Summary}";
}
