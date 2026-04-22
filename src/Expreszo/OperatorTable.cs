using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace Expreszo;

/// <summary>
/// Reference-identity comparer for <see cref="ExprFunc"/> delegates.
/// <see cref="ReferenceEqualityComparer.Instance"/> only implements
/// <c>IEqualityComparer&lt;object&gt;</c>, so it doesn't satisfy the generic
/// <c>IEqualityComparer&lt;ExprFunc&gt;</c> that <see cref="HashSet{T}"/> and
/// <c>ToFrozenSet</c> expect.
/// </summary>
internal sealed class ExprFuncReferenceComparer : IEqualityComparer<ExprFunc>
{
    public static readonly ExprFuncReferenceComparer Instance = new();

    private ExprFuncReferenceComparer() { }

    public bool Equals(ExprFunc? x, ExprFunc? y) => ReferenceEquals(x, y);

    public int GetHashCode(ExprFunc obj) => RuntimeHelpers.GetHashCode(obj);
}

/// <summary>
/// Container holding the implementations the evaluator dispatches to: unary
/// prefix ops, binary ops, ternary ops, and regular functions. Built once by
/// the <see cref="Parser"/> constructor and shared across every
/// <see cref="Expression"/> it produces.
/// </summary>
internal sealed class OperatorTable(
    FrozenDictionary<string, ExprFunc> unaryOps,
    FrozenDictionary<string, ExprFunc> binaryOps,
    FrozenDictionary<string, ExprFunc> ternaryOps,
    FrozenDictionary<string, ExprFunc> functions,
    FrozenSet<string> asyncFunctionNames,
    FrozenSet<ExprFunc> callableImplementations
)
{
    public FrozenDictionary<string, ExprFunc> UnaryOps { get; } = unaryOps;
    public FrozenDictionary<string, ExprFunc> BinaryOps { get; } = binaryOps;
    public FrozenDictionary<string, ExprFunc> TernaryOps { get; } = ternaryOps;
    public FrozenDictionary<string, ExprFunc> Functions { get; } = functions;
    public FrozenSet<string> AsyncFunctionNames { get; } = asyncFunctionNames;

    /// <summary>
    /// All <see cref="ExprFunc"/> delegates a user expression may invoke via
    /// <c>Call</c> (regular functions plus unary ops, which resolve to
    /// first-class function values). Keyed by reference identity so the
    /// allow-list check runs in O(1) instead of scanning dictionaries.
    /// </summary>
    public FrozenSet<ExprFunc> CallableImplementations { get; } = callableImplementations;
}

/// <summary>
/// Mutable builder for <see cref="OperatorTable"/>. The <see cref="Parser"/>
/// constructor prepopulates it with the built-in preset before building the
/// frozen table.
/// </summary>
internal sealed class OperatorTableBuilder
{
    private readonly Dictionary<string, ExprFunc> _unary = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExprFunc> _binary = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExprFunc> _ternary = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExprFunc> _functions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _asyncFunctionNames = new(StringComparer.Ordinal);

    public OperatorTableBuilder AddUnary(string op, ExprFunc impl)
    {
        _unary[op] = impl;
        return this;
    }

    public OperatorTableBuilder AddBinary(string op, ExprFunc impl)
    {
        _binary[op] = impl;
        return this;
    }

    public OperatorTableBuilder AddTernary(string op, ExprFunc impl)
    {
        _ternary[op] = impl;
        return this;
    }

    public OperatorTableBuilder AddFunction(string name, ExprFunc impl, bool isAsync = false)
    {
        _functions[name] = impl;
        if (isAsync)
        {
            _asyncFunctionNames.Add(name);
        }
        else
        {
            _asyncFunctionNames.Remove(name);
        }
        return this;
    }

    /// <summary>Sync helper: wraps a plain <see cref="Func{T1, TResult}"/> as an <see cref="ExprFunc"/>.</summary>
    public static ExprFunc Sync(Func<Value[], Value> impl) =>
        (args, _) => ValueTask.FromResult(impl(args));

    public OperatorTable Build()
    {
        // Only Functions and UnaryOps are reachable from Call AST nodes. Binary
        // and ternary ops dispatch via their own operators and never flow
        // through the allow-list check, so don't include them here.
        HashSet<ExprFunc> callable = new(ExprFuncReferenceComparer.Instance);
        foreach (ExprFunc v in _functions.Values)
        {
            callable.Add(v);
        }
        foreach (ExprFunc v in _unary.Values)
        {
            callable.Add(v);
        }

        return new OperatorTable(
            _unary.ToFrozenDictionary(StringComparer.Ordinal),
            _binary.ToFrozenDictionary(StringComparer.Ordinal),
            _ternary.ToFrozenDictionary(StringComparer.Ordinal),
            _functions.ToFrozenDictionary(StringComparer.Ordinal),
            _asyncFunctionNames.ToFrozenSet(StringComparer.Ordinal),
            callable.ToFrozenSet(ExprFuncReferenceComparer.Instance)
        );
    }
}
