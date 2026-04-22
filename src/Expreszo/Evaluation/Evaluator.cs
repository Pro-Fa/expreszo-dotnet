using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Builtins;
using Expreszo.Errors;
using Expreszo.Parsing;

// CA1859 asks for concrete return types on private helpers. The evaluator's
// helpers all flow into Value-typed call sites where the concrete variant
// gets upcast anyway, so the suggestion doesn't help.
#pragma warning disable CA1859

namespace Expreszo.Evaluation;

/// <summary>
/// Single-walker AST evaluator. Returns <see cref="ValueTask{T}"/> uniformly
/// so sync and async functions share one code path; when every step
/// completes synchronously the state machine is never allocated.
/// </summary>
internal sealed class Evaluator
{
    private readonly OperatorTable _ops;
    private readonly ParserConfig _config;

    // Unique counter for inline-lambda names stored in scope.
    private int _lambdaCounter;

    public Evaluator(OperatorTable ops, ParserConfig config)
    {
        _ops = ops;
        _config = config;
    }

    public async ValueTask<Value> EvaluateAsync(Node root, EvalContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        var result = await EvalNode(root, ctx).ConfigureAwait(false);
        return NormalizeNegativeZero(result);
    }

    private ValueTask<Value> EvalNode(Node node, EvalContext ctx)
    {
        return node switch
        {
            NumberLit n => ValueTask.FromResult<Value>(Value.Number.Of(n.Value)),
            StringLit s => ValueTask.FromResult<Value>(new Value.String(s.Value)),
            BoolLit b => ValueTask.FromResult<Value>(Value.Boolean.Of(b.Value)),
            NullLit => ValueTask.FromResult<Value>(Value.Null.Instance),
            UndefinedLit => ValueTask.FromResult<Value>(Value.Undefined.Instance),
            RawLit r => ValueTask.FromResult<Value>(r.Value),
            Paren p => EvalNode(p.Inner, ctx),
            Ident id => ValueTask.FromResult(ResolveIdent(id.Name, ctx)),
            NameRef nr => ValueTask.FromResult<Value>(new Value.String(nr.Name)),
            Member m => EvalMember(m, ctx),
            Unary u => EvalUnary(u, ctx),
            Binary b => EvalBinary(b, ctx),
            Ternary t => EvalTernary(t, ctx),
            Call c => EvalCall(c, ctx),
            Lambda l => ValueTask.FromResult(EvalLambda(l, ctx)),
            FunctionDef fd => ValueTask.FromResult(EvalFunctionDef(fd, ctx)),
            Case k => EvalCase(k, ctx),
            Sequence s => EvalSequence(s, ctx),
            ArrayLit a => EvalArrayLit(a, ctx),
            ObjectLit o => EvalObjectLit(o, ctx),
            _ => throw new EvaluationException($"unhandled AST node: {node.GetType().Name}"),
        };
    }

    // ---------- variable resolution ----------

    private Value ResolveIdent(string name, EvalContext ctx)
    {
        // 1. Built-in functions
        if (_ops.Functions.TryGetValue(name, out var fn))
        {
            return new Value.Function(fn, name);
        }
        // 2. Unary ops (allows bare `sin` etc. to resolve to the function)
        if (_ops.UnaryOps.TryGetValue(name, out var un))
        {
            return new Value.Function((args, c) => un(args, c), name);
        }
        // 3. Local / parent scope
        if (ctx.Scope.TryGet(name, out var local))
        {
            return local;
        }
        // 4. Per-call resolver
        if (ctx.Resolver is not null)
        {
            var r = ctx.Resolver(name);
            var resolved = FromResolve(r, ctx);
            if (resolved is not null) return resolved;
        }
        // 5. Numeric constants (PI etc.) — last so callers can shadow.
        if (_config.NumericConstants.TryGetValue(name, out var nv))
        {
            return Value.Number.Of(nv);
        }
        throw new VariableException(name);
    }

    private Value? FromResolve(VariableResolveResult? r, EvalContext ctx)
    {
        if (r is null) return null;
        if (r is VariableResolveResult.NotResolvedResult) return null;
        if (r is VariableResolveResult.Bound b) return b.Value;
        if (r is VariableResolveResult.Alias a) return ResolveIdent(a.Name, ctx);
        return null;
    }

    // ---------- member / unary / binary / ternary ----------

    private async ValueTask<Value> EvalMember(Member m, EvalContext ctx)
    {
        var obj = await EvalNode(m.Object, ctx).ConfigureAwait(false);
        return obj switch
        {
            Value.Null or Value.Undefined => Value.Undefined.Instance,
            Value.Object o => o.Props.TryGetValue(m.Property, out var v) ? v : Value.Undefined.Instance,
            _ => Value.Undefined.Instance,
        };
    }

    private async ValueTask<Value> EvalUnary(Unary u, EvalContext ctx)
    {
        var operand = await EvalNode(u.Operand, ctx).ConfigureAwait(false);
        if (!_ops.UnaryOps.TryGetValue(u.Op, out var fn))
        {
            throw new FunctionException(u.Op);
        }
        return await fn([operand], ctx).ConfigureAwait(false);
    }

    private async ValueTask<Value> EvalBinary(Binary b, EvalContext ctx)
    {
        // Assignment: LHS must be NameRef; store in scope.
        if (b.Op == "=")
        {
            if (b.Left is not NameRef nr)
            {
                throw new EvaluationException(Messages.AssignmentToNonIdentifier());
            }
            var value = await EvalNode(b.Right, ctx).ConfigureAwait(false);
            ctx.Scope.Assign(nr.Name, value);
            return value;
        }

        // Short-circuit and / && / or / ||. RHS is wrapped in Paren by the
        // parser but the inner evaluation is gated here.
        if (b.Op is "and" or "&&")
        {
            var left = await EvalNode(b.Left, ctx).ConfigureAwait(false);
            if (!left.IsTruthy()) return Value.Boolean.False;
            var right = await EvalNode(b.Right, ctx).ConfigureAwait(false);
            return Value.Boolean.Of(right.IsTruthy());
        }
        if (b.Op is "or" or "||")
        {
            var left = await EvalNode(b.Left, ctx).ConfigureAwait(false);
            if (left.IsTruthy()) return Value.Boolean.True;
            var right = await EvalNode(b.Right, ctx).ConfigureAwait(false);
            return Value.Boolean.Of(right.IsTruthy());
        }

        var l = await EvalNode(b.Left, ctx).ConfigureAwait(false);
        var r = await EvalNode(b.Right, ctx).ConfigureAwait(false);
        if (!_ops.BinaryOps.TryGetValue(b.Op, out var fn))
        {
            throw new FunctionException(b.Op);
        }
        return await fn([l, r], ctx).ConfigureAwait(false);
    }

    private async ValueTask<Value> EvalTernary(Ternary t, EvalContext ctx)
    {
        if (t.Op == "?")
        {
            var cond = await EvalNode(t.A, ctx).ConfigureAwait(false);
            return cond.IsTruthy()
                ? await EvalNode(t.B, ctx).ConfigureAwait(false)
                : await EvalNode(t.C, ctx).ConfigureAwait(false);
        }
        var a = await EvalNode(t.A, ctx).ConfigureAwait(false);
        var b = await EvalNode(t.B, ctx).ConfigureAwait(false);
        var c = await EvalNode(t.C, ctx).ConfigureAwait(false);
        if (!_ops.TernaryOps.TryGetValue(t.Op, out var fn))
        {
            throw new FunctionException(t.Op);
        }
        return await fn([a, b, c], ctx).ConfigureAwait(false);
    }

    // ---------- call / lambda / function-def ----------

    private async ValueTask<Value> EvalCall(Call c, EvalContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        // Lazy `if(cond, t, f)` — only the selected branch is evaluated.
        if (c.Callee is Ident idf && idf.Name == "if" && c.Args.Length == 3)
        {
            var cond = await EvalNode(c.Args[0], ctx).ConfigureAwait(false);
            return cond.IsTruthy()
                ? await EvalNode(c.Args[1], ctx).ConfigureAwait(false)
                : await EvalNode(c.Args[2], ctx).ConfigureAwait(false);
        }

        var callee = await EvalNode(c.Callee, ctx).ConfigureAwait(false);
        if (callee is not Value.Function fn)
        {
            throw new FunctionException(
                c.Callee is Ident id ? id.Name : "<expression>",
                message: Messages.NotCallable(c.Callee is Ident nid ? nid.Name : "<expression>"));
        }

        var args = new Value[c.Args.Length];
        for (var i = 0; i < c.Args.Length; i++)
        {
            args[i] = await EvalNode(c.Args[i], ctx).ConfigureAwait(false);
        }
        return await fn.Invoke(args, ctx).ConfigureAwait(false);
    }

    private Value EvalLambda(Lambda l, EvalContext ctx)
    {
        var captured = ctx.Scope;
        var paramsArr = l.Params;
        var body = l.Body;

        ExprFunc impl = async (args, _) =>
        {
            var child = captured.CreateChild();
            for (var i = 0; i < paramsArr.Length; i++)
            {
                child.SetLocal(paramsArr[i], i < args.Length ? args[i] : Value.Undefined.Instance);
            }
            var inner = new EvalContext(child, ctx.ErrorHandler, ctx.Resolver, ctx.CancellationToken);
            return await EvalNode(body, inner).ConfigureAwait(false);
        };

        var name = $"__lambda_{System.Threading.Interlocked.Increment(ref _lambdaCounter)}__";
        return new Value.Function(impl, name);
    }

    private Value EvalFunctionDef(FunctionDef fd, EvalContext ctx)
    {
        var fn = EvalLambda(new Lambda(fd.Params, fd.Body, fd.Span), ctx);
        ctx.Scope.Assign(fd.Name, fn);
        return fn;
    }

    // ---------- control flow ----------

    private async ValueTask<Value> EvalCase(Case k, EvalContext ctx)
    {
        if (k.Subject is not null)
        {
            var subject = await EvalNode(k.Subject, ctx).ConfigureAwait(false);
            foreach (var arm in k.Arms)
            {
                var when = await EvalNode(arm.When, ctx).ConfigureAwait(false);
                if (CorePreset.StrictEquals(subject, when))
                {
                    return await EvalNode(arm.Then, ctx).ConfigureAwait(false);
                }
            }
        }
        else
        {
            foreach (var arm in k.Arms)
            {
                var when = await EvalNode(arm.When, ctx).ConfigureAwait(false);
                if (when.IsTruthy())
                {
                    return await EvalNode(arm.Then, ctx).ConfigureAwait(false);
                }
            }
        }
        if (k.Else is not null)
        {
            return await EvalNode(k.Else, ctx).ConfigureAwait(false);
        }
        return Value.Undefined.Instance;
    }

    private async ValueTask<Value> EvalSequence(Sequence s, EvalContext ctx)
    {
        Value last = Value.Undefined.Instance;
        foreach (var stmt in s.Statements)
        {
            last = await EvalNode(stmt, ctx).ConfigureAwait(false);
        }
        return last;
    }

    // ---------- array / object literals with spread ----------

    private async ValueTask<Value> EvalArrayLit(ArrayLit a, EvalContext ctx)
    {
        var builder = ImmutableArray.CreateBuilder<Value>(a.Elements.Length);
        foreach (var entry in a.Elements)
        {
            switch (entry)
            {
                case ArrayElement e:
                    builder.Add(await EvalNode(e.Node, ctx).ConfigureAwait(false));
                    break;
                case ArraySpread sp:
                    var v = await EvalNode(sp.Argument, ctx).ConfigureAwait(false);
                    if (v is Value.Array arr)
                    {
                        builder.AddRange(arr.Items);
                    }
                    else
                    {
                        throw new EvaluationException("spread target in array literal must be an array");
                    }
                    break;
            }
        }
        return new Value.Array(builder.ToImmutable());
    }

    private async ValueTask<Value> EvalObjectLit(ObjectLit o, EvalContext ctx)
    {
        var dict = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (var entry in o.Properties)
        {
            switch (entry)
            {
                case ObjectProperty p:
                    dict[p.Key] = await EvalNode(p.Value, ctx).ConfigureAwait(false);
                    break;
                case ObjectSpread sp:
                    var v = await EvalNode(sp.Argument, ctx).ConfigureAwait(false);
                    if (v is Value.Object obj)
                    {
                        foreach (var kv in obj.Props)
                        {
                            dict[kv.Key] = kv.Value;
                        }
                    }
                    else
                    {
                        throw new EvaluationException("spread target in object literal must be an object");
                    }
                    break;
            }
        }
        return Value.Object.From(dict);
    }

    // ---------- post-processing ----------

    private static Value NormalizeNegativeZero(Value v) =>
        v is Value.Number n && n.V == 0 && double.IsNegative(n.V) ? Value.Number.Of(0) : v;
}
