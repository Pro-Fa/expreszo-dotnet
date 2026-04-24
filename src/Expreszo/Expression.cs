using System.Text.Json;
using Expreszo.Ast;
using Expreszo.Ast.Visitors;
using Expreszo.Errors;
using Expreszo.Evaluation;
using Expreszo.Parsing;

namespace Expreszo;

/// <summary>
/// A parsed, reusable expression. Immutable after construction. Evaluate
/// many times with different variable bindings, or call <see cref="Simplify"/>
/// to collapse constant subtrees before evaluation.
/// </summary>
/// <remarks>
/// Safe to share across threads. Each concurrent evaluation must supply its
/// own variable bindings - the internal <see cref="Scope"/> is built per call.
/// </remarks>
public sealed class Expression
{
    private readonly Node _root;
    private readonly OperatorTable _ops;
    private readonly ParserConfig _config;

    private string? _cachedToString;

    internal Expression(Node root, OperatorTable ops, ParserConfig config)
    {
        _root = root;
        _ops = ops;
        _config = config;
    }

    /// <summary>
    /// The parsed AST root. Exposed so analysis tools (e.g. the
    /// <c>Expreszo.Analysis</c> package) can walk the tree without a
    /// second parse pass.
    /// </summary>
    public Node Root => _root;

    internal OperatorTable Ops => _ops;
    internal ParserConfig Config => _config;

    /// <summary>
    /// Synchronous evaluation. Throws <see cref="AsyncRequiredException"/>
    /// when the expression calls a function whose result is not synchronously
    /// completed - use <see cref="EvaluateAsync"/> instead.
    /// </summary>
    public Value Evaluate(JsonDocument? values = null, VariableResolver? resolver = null)
    {
        ValueTask<Value> task = EvaluateAsync(values, resolver, CancellationToken.None);
        if (task.IsCompletedSuccessfully)
        {
            return task.Result;
        }
        if (task.IsCompleted)
        {
            // Faulted or cancelled synchronously - let the underlying exception
            // propagate instead of masking it as AsyncRequired.
            return task.GetAwaiter().GetResult();
        }
        // Still running - the expression needs the async path.
        throw new AsyncRequiredException();
    }

    /// <summary>Asynchronous evaluation.</summary>
    public ValueTask<Value> EvaluateAsync(
        JsonDocument? values = null,
        VariableResolver? resolver = null,
        CancellationToken cancellationToken = default
    )
    {
        Scope scope = Scope.FromJsonDocument(values);
        var ctx = new EvalContext(
            scope,
            ThrowingErrorHandler.Instance,
            resolver,
            cancellationToken
        );
        var evaluator = new Evaluator(_ops, _config);
        return evaluator.EvaluateAsync(_root, ctx);
    }

    /// <summary>Collapses constant subtrees to literal nodes.</summary>
    public Expression Simplify(JsonDocument? values = null)
    {
        var visitor = new SimplifyVisitor(_ops, _config, values);
        Node newRoot = visitor.Simplify(_root);
        return new Expression(newRoot, _ops, _config);
    }

    /// <summary>Replaces every <see cref="Ident"/> matching <paramref name="variable"/> with <paramref name="expr"/>.</summary>
    public Expression Substitute(string variable, Expression expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var visitor = new SubstituteVisitor(variable, expr._root);
        Node newRoot = visitor.Substitute(_root);
        return new Expression(newRoot, _ops, _config);
    }

    /// <summary>Parses <paramref name="expression"/> and substitutes it for every matching variable.</summary>
    public Expression Substitute(string variable, string expression)
    {
        Node parsed = Parsing.PrattParser.Parse(_config, expression);
        return Substitute(variable, new Expression(parsed, _ops, _config));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _cachedToString ??= ToStringVisitor.Run(_root);
    }

    /// <summary>Every symbol referenced by the expression (variables + functions, as seen).</summary>
    public IReadOnlyList<string> Symbols(bool withMembers = false) =>
        new SymbolsVisitor(withMembers).Collect(_root);

    /// <summary>Subset of <see cref="Symbols"/> that aren't registered as functions or operators.</summary>
    public IReadOnlyList<string> Variables(bool withMembers = false)
    {
        IReadOnlyList<string> all = Symbols(withMembers);
        var result = new List<string>(all.Count);
        foreach (string s in all)
        {
            if (_ops.Functions.ContainsKey(s))
            {
                continue;
            }

            if (_ops.UnaryOps.ContainsKey(s))
            {
                continue;
            }

            result.Add(s);
        }
        return result;
    }

    /// <summary>Applies a visitor to the AST root.</summary>
    public T Accept<T>(INodeVisitor<T> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        return visitor.Visit(_root);
    }
}
