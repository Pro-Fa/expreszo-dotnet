using System.Collections.Immutable;
using System.Text.Json;
using Expreszo.Errors;
using Expreszo.Evaluation;
using Expreszo.Parsing;

namespace Expreszo.Ast.Visitors;

/// <summary>
/// Constant-folding visitor. Walks the AST and collapses sub-expressions
/// whose operands are all literals (or variables resolvable from a supplied
/// JsonDocument) into pre-computed literal nodes.
/// </summary>
internal sealed class SimplifyVisitor
{
    private readonly OperatorTable _ops;
    private readonly ParserConfig _config;
    private readonly Scope _scope;

    public SimplifyVisitor(OperatorTable ops, ParserConfig config, JsonDocument? values)
    {
        _ops = ops;
        _config = config;
        _scope = Scope.FromJsonDocument(values);
    }

    public Node Simplify(Node node) => node switch
    {
        // Literals pass through.
        NumberLit or StringLit or BoolLit or NullLit or UndefinedLit or RawLit => node,
        NameRef => node,

        Paren p => new Paren(Simplify(p.Inner), p.Span),

        Ident id => TryResolveIdent(id),

        Unary u => TrySimplifyUnary(u),
        Binary b => TrySimplifyBinary(b),
        Ternary t => TrySimplifyTernary(t),
        Member m => TrySimplifyMember(m),

        // Calls / lambdas / function defs / arrays / objects / case / sequence:
        // recurse into subtrees but don't fold at this level.
        Call c => new Call(Simplify(c.Callee), [.. c.Args.Select(Simplify)], c.Span),
        Lambda l => new Lambda(l.Params, Simplify(l.Body), l.Span),
        FunctionDef fd => new FunctionDef(fd.Name, fd.Params, Simplify(fd.Body), fd.Span),
        ArrayLit a => new ArrayLit(a.Elements.Select<ArrayEntry, ArrayEntry>(e => e switch
        {
            ArrayElement el => new ArrayElement(Simplify(el.Node), el.Span),
            ArraySpread sp => new ArraySpread(Simplify(sp.Argument), sp.Span),
            _ => e,
        }).ToImmutableArray(), a.Span),
        ObjectLit o => new ObjectLit(o.Properties.Select<ObjectEntry, ObjectEntry>(p => p switch
        {
            ObjectProperty pr => new ObjectProperty(pr.Key, Simplify(pr.Value), pr.Quoted, pr.Span),
            ObjectSpread sp => new ObjectSpread(Simplify(sp.Argument), sp.Span),
            _ => p,
        }).ToImmutableArray(), o.Span),
        Sequence s => new Sequence([.. s.Statements.Select(Simplify)], s.Span),
        Case k => SimplifyCase(k),

        _ => node,
    };

    private Node TryResolveIdent(Ident id)
    {
        if (_scope.TryGet(id.Name, out var v))
        {
            return LiteralFor(v, id.Span);
        }
        if (_config.NumericConstants.TryGetValue(id.Name, out var nv))
        {
            return new NumberLit(nv, id.Span);
        }
        return id;
    }

    private Node TrySimplifyUnary(Unary u)
    {
        var operand = Simplify(u.Operand);
        if (operand is not (NumberLit or StringLit or BoolLit or NullLit or UndefinedLit))
        {
            return new Unary(u.Op, operand, u.Span);
        }
        if (!_ops.UnaryOps.TryGetValue(u.Op, out var fn))
        {
            return new Unary(u.Op, operand, u.Span);
        }
        try
        {
            var v = ValueFor(operand);
            var task = fn([v], DummyCtx);
            if (!task.IsCompletedSuccessfully) return new Unary(u.Op, operand, u.Span);
            return LiteralFor(task.Result, u.Span);
        }
        catch (ExpressionException)
        {
            // Keep the sub-expression as-is; runtime evaluation will surface the error.
            return new Unary(u.Op, operand, u.Span);
        }
    }

    private Node TrySimplifyBinary(Binary b)
    {
        var left = Simplify(b.Left);
        var right = Simplify(b.Right);

        // Don't fold assignments or short-circuits.
        if (b.Op is "=" or "and" or "&&" or "or" or "||")
        {
            return new Binary(b.Op, left, right, b.Span);
        }
        if (!IsLiteral(left) || !IsLiteral(right))
        {
            return new Binary(b.Op, left, right, b.Span);
        }
        if (!_ops.BinaryOps.TryGetValue(b.Op, out var fn))
        {
            return new Binary(b.Op, left, right, b.Span);
        }
        try
        {
            var task = fn([ValueFor(left), ValueFor(right)], DummyCtx);
            if (!task.IsCompletedSuccessfully) return new Binary(b.Op, left, right, b.Span);
            return LiteralFor(task.Result, b.Span);
        }
        catch (ExpressionException)
        {
            return new Binary(b.Op, left, right, b.Span);
        }
    }

    private Node TrySimplifyTernary(Ternary t)
    {
        var a = Simplify(t.A);
        var b = Simplify(t.B);
        var c = Simplify(t.C);
        if (t.Op == "?" && IsLiteral(a))
        {
            // Unwrap the Paren wrappers the parser adds to the branches so the
            // chosen side replaces the whole ternary cleanly.
            var chosen = ValueFor(a).IsTruthy() ? Unwrap(b) : Unwrap(c);
            return chosen;
        }
        return new Ternary(t.Op, a, b, c, t.Span);
    }

    private Node TrySimplifyMember(Member m)
    {
        var obj = Simplify(m.Object);
        if (obj is RawLit r && r.Value is Value.Object o && o.Props.TryGetValue(m.Property, out var v))
        {
            return LiteralFor(v, m.Span);
        }
        return new Member(obj, m.Property, m.Span);
    }

    private Case SimplifyCase(Case k) => new(
        k.Subject is null ? null : Simplify(k.Subject),
        k.Arms.Select(arm => new CaseArm(Simplify(arm.When), Simplify(arm.Then))).ToImmutableArray(),
        k.Else is null ? null : Simplify(k.Else),
        k.Span);

    // ---------- helpers ----------

    private static Node Unwrap(Node n) => n is Paren p ? Unwrap(p.Inner) : n;

    private static bool IsLiteral(Node n) =>
        n is NumberLit or StringLit or BoolLit or NullLit or UndefinedLit or RawLit;

    private static Value ValueFor(Node n) => n switch
    {
        NumberLit nn => Value.Number.Of(nn.Value),
        StringLit s => new Value.String(s.Value),
        BoolLit b => Value.Boolean.Of(b.Value),
        NullLit => Value.Null.Instance,
        UndefinedLit => Value.Undefined.Instance,
        RawLit r => r.Value,
        _ => throw new InvalidOperationException("ValueFor called with non-literal"),
    };

    private static Node LiteralFor(Value v, TextSpan span) => v switch
    {
        Value.Number n => new NumberLit(n.V, span),
        Value.String s => new StringLit(s.V, span),
        Value.Boolean b => new BoolLit(b.V, span),
        Value.Null => new NullLit(span),
        Value.Undefined => new UndefinedLit(span),
        _ => new RawLit(v, span),
    };

    // Dummy EvalContext used only for constant folding — we never evaluate
    // anything that would touch scope, resolver, or cancellation through it.
    private static readonly EvalContext DummyCtx = new(
        new Scope(),
        ThrowingErrorHandler.Instance);
}
