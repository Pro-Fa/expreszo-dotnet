using System.Collections.Frozen;

namespace Expreszo.Parsing;

/// <summary>
/// Static configuration driving the tokenizer: which names are keywords or
/// named operators, which constants expand to numeric values, and which
/// operators are currently enabled. In Phase 3 the <c>Parser</c> class builds
/// one of these from its <c>ParserOptions</c>; for Phase 2 tests we build
/// them manually.
/// </summary>
/// <remarks>
/// <para>
/// Operator enablement is a predicate rather than a flag set because the
/// original library allows callers to pass arbitrary operator gates via
/// <c>ParserOptions.operators</c>. The port keeps that extension point for
/// internal use even though public operator customisation is out of scope.
/// </para>
/// <para>Instances are immutable and cheap to share across tokenizer runs.</para>
/// </remarks>
internal sealed class TokenizerConfig
{
    public TokenizerConfig(
        FrozenSet<string> keywords,
        FrozenSet<string> unaryOps,
        FrozenSet<string> binaryOps,
        FrozenSet<string> ternaryOps,
        FrozenDictionary<string, double> numericConstants,
        FrozenDictionary<string, Value> builtinLiterals,
        Func<string, bool> isOperatorEnabled)
    {
        Keywords = keywords;
        UnaryOps = unaryOps;
        BinaryOps = binaryOps;
        TernaryOps = ternaryOps;
        NumericConstants = numericConstants;
        BuiltinLiterals = builtinLiterals;
        IsOperatorEnabled = isOperatorEnabled;
    }

    public FrozenSet<string> Keywords { get; }
    public FrozenSet<string> UnaryOps { get; }
    public FrozenSet<string> BinaryOps { get; }
    public FrozenSet<string> TernaryOps { get; }
    public FrozenDictionary<string, double> NumericConstants { get; }
    public FrozenDictionary<string, Value> BuiltinLiterals { get; }
    public Func<string, bool> IsOperatorEnabled { get; }

    public bool IsNamedOperator(string name) =>
        UnaryOps.Contains(name) || BinaryOps.Contains(name) || TernaryOps.Contains(name);

    /// <summary>
    /// Default configuration used during Phase 2 lexer tests. Enables every
    /// operator, registers every expreszo built-in literal (<c>true</c>,
    /// <c>false</c>, <c>null</c>) and numeric constant (<c>PI</c>, <c>E</c>,
    /// <c>Infinity</c>, <c>NaN</c>), and lists every named operator the
    /// library's parser recognises so lexing stays self-contained.
    /// </summary>
    // Declared BEFORE Default so they're initialized by the time Build() runs.
    // CA1861 flags repeated array allocation; these fields dodge that noise.
    private static readonly string[] DefaultKeywords =
        ["case", "when", "then", "else", "end"];

    private static readonly string[] DefaultUnaryOps =
    [
        "not",
        "abs", "ceil", "floor", "round", "sign",
        "sqrt", "cbrt", "trunc",
        "exp", "expm1",
        "log", "ln", "log1p", "log2", "log10", "lg",
        "sin", "cos", "tan",
        "asin", "acos", "atan",
        "sinh", "cosh", "tanh",
        "asinh", "acosh", "atanh",
        "length",
    ];

    private static readonly string[] DefaultBinaryOps =
    [
        "and", "or", "in", "not in", "as",
    ];

    public static TokenizerConfig Default { get; } = Build();

    private static TokenizerConfig Build()
    {
        var keywords = DefaultKeywords.ToFrozenSet(StringComparer.Ordinal);
        var unary = DefaultUnaryOps.ToFrozenSet(StringComparer.Ordinal);
        var binary = DefaultBinaryOps.ToFrozenSet(StringComparer.Ordinal);
        var ternary = FrozenSet<string>.Empty;

        var numericConstants = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["PI"] = Math.PI,
            ["E"] = Math.E,
            ["Infinity"] = double.PositiveInfinity,
            ["NaN"] = double.NaN,
        }.ToFrozenDictionary(StringComparer.Ordinal);

        var literals = new Dictionary<string, Value>(StringComparer.Ordinal)
        {
            ["true"] = Value.Boolean.True,
            ["false"] = Value.Boolean.False,
            ["null"] = Value.Null.Instance,
        }.ToFrozenDictionary(StringComparer.Ordinal);

        return new TokenizerConfig(
            keywords,
            unary,
            binary,
            ternary,
            numericConstants,
            literals,
            _ => true);
    }
}
