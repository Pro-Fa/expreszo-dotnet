using System.Collections.Frozen;

namespace Expreszo.Parsing;

/// <summary>
/// Static configuration driving both the tokenizer and the Pratt parser:
/// keywords, named operators, numeric constants, built-in literals, and the
/// operator-enablement predicate. The public <see cref="Parser"/> builds
/// one of these from its <see cref="ParserOptions"/>.
/// </summary>
/// <remarks>
/// Operator enablement is a predicate rather than a flag set so operator
/// gating can be driven by arbitrary logic without expanding the option
/// surface. Instances are immutable and cheap to share across runs.
/// </remarks>
internal sealed class ParserConfig(
    FrozenSet<string> keywords,
    FrozenSet<string> unaryOps,
    FrozenSet<string> binaryOps,
    FrozenSet<string> ternaryOps,
    FrozenDictionary<string, double> numericConstants,
    FrozenDictionary<string, Value> builtinLiterals,
    Func<string, bool> isOperatorEnabled,
    bool allowMemberAccess = true
)
{
    public FrozenSet<string> Keywords { get; } = keywords;
    public FrozenSet<string> UnaryOps { get; } = unaryOps;
    public FrozenSet<string> BinaryOps { get; } = binaryOps;
    public FrozenSet<string> TernaryOps { get; } = ternaryOps;
    public FrozenDictionary<string, double> NumericConstants { get; } = numericConstants;
    public FrozenDictionary<string, Value> BuiltinLiterals { get; } = builtinLiterals;
    public Func<string, bool> IsOperatorEnabled { get; } = isOperatorEnabled;

    /// <summary>Whether member access (<c>.</c>) is permitted. Defaults to <c>true</c>.</summary>
    public bool AllowMemberAccess { get; } = allowMemberAccess;

    public bool IsNamedOperator(string name) =>
        UnaryOps.Contains(name) || BinaryOps.Contains(name) || TernaryOps.Contains(name);

    public bool IsPrefixOperator(string name) => UnaryOps.Contains(name);

    // Declared BEFORE Default so they're initialized by the time Build() runs.
    // CA1861 flags repeated array allocation; these fields dodge that noise.
    private static readonly string[] DefaultKeywords = ["case", "when", "then", "else", "end"];

    private static readonly string[] DefaultUnaryOps =
    [
        // Symbolic prefix unaries - the tokenizer emits these as single-char
        // operators; they also need to be in UnaryOps so the Pratt parser's
        // IsPrefixOperator check accepts them.
        "-",
        "+",
        "!",
        "not",
        "abs",
        "ceil",
        "floor",
        "round",
        "sign",
        "sqrt",
        "cbrt",
        "trunc",
        "exp",
        "expm1",
        "log",
        "ln",
        "log1p",
        "log2",
        "log10",
        "lg",
        "sin",
        "cos",
        "tan",
        "asin",
        "acos",
        "atan",
        "sinh",
        "cosh",
        "tanh",
        "asinh",
        "acosh",
        "atanh",
        "length",
    ];

    private static readonly string[] DefaultBinaryOps = ["and", "or", "in", "not in", "as"];

    /// <summary>
    /// Default configuration used during parser tests. Enables every operator,
    /// registers every expreszo built-in literal and numeric constant, and
    /// lists every named operator the library's parser recognises.
    /// </summary>
    public static ParserConfig Default { get; } = Build();

    private static ParserConfig Build()
    {
        FrozenSet<string> keywords = DefaultKeywords.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> unary = DefaultUnaryOps.ToFrozenSet(StringComparer.Ordinal);
        FrozenSet<string> binary = DefaultBinaryOps.ToFrozenSet(StringComparer.Ordinal);
        var ternary = FrozenSet<string>.Empty;

        FrozenDictionary<string, double> numericConstants = new Dictionary<string, double>(
            StringComparer.Ordinal
        )
        {
            ["PI"] = Math.PI,
            ["E"] = Math.E,
            ["Infinity"] = double.PositiveInfinity,
            ["NaN"] = double.NaN,
        }.ToFrozenDictionary(StringComparer.Ordinal);

        FrozenDictionary<string, Value> literals = new Dictionary<string, Value>(
            StringComparer.Ordinal
        )
        {
            ["true"] = Value.Boolean.True,
            ["false"] = Value.Boolean.False,
            ["null"] = Value.Null.Instance,
        }.ToFrozenDictionary(StringComparer.Ordinal);

        return new ParserConfig(
            keywords,
            unary,
            binary,
            ternary,
            numericConstants,
            literals,
            _ => true,
            allowMemberAccess: true
        );
    }
}
