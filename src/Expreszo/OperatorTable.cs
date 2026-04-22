using System.Collections.Frozen;

namespace Expreszo;

/// <summary>
/// Container holding the implementations the evaluator dispatches to: unary
/// prefix ops, binary ops, ternary ops, and regular functions. Built once by
/// the <see cref="Parser"/> constructor and shared across every
/// <see cref="Expression"/> it produces.
/// </summary>
internal sealed class OperatorTable
{
    public OperatorTable(
        FrozenDictionary<string, ExprFunc> unaryOps,
        FrozenDictionary<string, ExprFunc> binaryOps,
        FrozenDictionary<string, ExprFunc> ternaryOps,
        FrozenDictionary<string, ExprFunc> functions,
        FrozenSet<string> asyncFunctionNames)
    {
        UnaryOps = unaryOps;
        BinaryOps = binaryOps;
        TernaryOps = ternaryOps;
        Functions = functions;
        AsyncFunctionNames = asyncFunctionNames;
    }

    public FrozenDictionary<string, ExprFunc> UnaryOps { get; }
    public FrozenDictionary<string, ExprFunc> BinaryOps { get; }
    public FrozenDictionary<string, ExprFunc> TernaryOps { get; }
    public FrozenDictionary<string, ExprFunc> Functions { get; }
    public FrozenSet<string> AsyncFunctionNames { get; }
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

    public OperatorTable Build() => new(
        _unary.ToFrozenDictionary(StringComparer.Ordinal),
        _binary.ToFrozenDictionary(StringComparer.Ordinal),
        _ternary.ToFrozenDictionary(StringComparer.Ordinal),
        _functions.ToFrozenDictionary(StringComparer.Ordinal),
        _asyncFunctionNames.ToFrozenSet(StringComparer.Ordinal));
}
