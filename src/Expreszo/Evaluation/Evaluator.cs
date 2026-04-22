using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Builtins;
using Expreszo.Errors;
using Expreszo.Parsing;
using Expreszo.Validation;

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
internal sealed class Evaluator(OperatorTable ops, ParserConfig config)
{
    // Unique counter for inline-lambda names stored in scope.
    private int _lambdaCounter;

    // Tracks nested Call depth to catch runaway recursion before the managed
    // stack overflows. StackOverflowException is uncatchable in .NET.
    private int _callDepth;

    public async ValueTask<Value> EvaluateAsync(Node root, EvalContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        Value result = await EvalNode(root, ctx).ConfigureAwait(false);
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
        ExpressionValidator.ValidateVariableName(name);
        // 1. Built-in functions
        if (ops.Functions.TryGetValue(name, out ExprFunc? fn))
        {
            return new Value.Function(fn, name);
        }
        // 2. Unary ops (allows bare `sin` etc. to resolve to the function).
        // Pass the delegate directly so the allow-list check can match it via
        // reference equality against _ops.UnaryOps entries below.
        if (ops.UnaryOps.TryGetValue(name, out ExprFunc? un))
        {
            return new Value.Function(un, name);
        }
        // 3. Local / parent scope
        if (ctx.Scope.TryGet(name, out Value? local))
        {
            return local;
        }
        // 4. Per-call resolver
        if (ctx.Resolver is not null)
        {
            VariableResolveResult r = ctx.Resolver(name);
            Value? resolved = FromResolve(r, ctx);
            if (resolved is not null)
            {
                return resolved;
            }
        }
        // 5. Numeric constants (PI etc.) - last so callers can shadow.
        if (config.NumericConstants.TryGetValue(name, out double nv))
        {
            return Value.Number.Of(nv);
        }
        throw new VariableException(name);
    }

    private Value? FromResolve(VariableResolveResult? r, EvalContext ctx) =>
        r switch
        {
            null => null,
            VariableResolveResult.Bound b => b.Value,
            VariableResolveResult.Alias a => ResolveIdent(a.Name, ctx),
            _ => null, // NotResolvedResult and any future unknowns
        };

    // ---------- member / unary / binary / ternary ----------

    private async ValueTask<Value> EvalMember(Member m, EvalContext ctx)
    {
        ExpressionValidator.ValidateMemberAccess(m.Property);
        Value obj = await EvalNode(m.Object, ctx).ConfigureAwait(false);
        return obj switch
        {
            Value.Null or Value.Undefined => Value.Undefined.Instance,
            Value.Object o => o.Props.TryGetValue(m.Property, out Value? v)
                ? v
                : Value.Undefined.Instance,
            _ => Value.Undefined.Instance,
        };
    }

    private async ValueTask<Value> EvalUnary(Unary u, EvalContext ctx)
    {
        Value operand = await EvalNode(u.Operand, ctx).ConfigureAwait(false);
        if (!ops.UnaryOps.TryGetValue(u.Op, out ExprFunc? fn))
        {
            throw new FunctionException(u.Op);
        }
        return await fn([operand], ctx).ConfigureAwait(false);
    }

    private ValueTask<Value> EvalBinary(Binary b, EvalContext ctx)
    {
        if (b.Op == "=")
        {
            return EvalAssignment(b, ctx);
        }
        if (b.Op is "and" or "&&" or "or" or "||")
        {
            return EvalShortCircuit(b, ctx);
        }
        return EvalGenericBinary(b, ctx);
    }

    private async ValueTask<Value> EvalAssignment(Binary b, EvalContext ctx)
    {
        if (b.Left is not NameRef nr)
        {
            throw new EvaluationException(Messages.AssignmentToNonIdentifier());
        }
        Value value = await EvalNode(b.Right, ctx).ConfigureAwait(false);
        ctx.Scope.Assign(nr.Name, value);
        return value;
    }

    private async ValueTask<Value> EvalShortCircuit(Binary b, EvalContext ctx)
    {
        // The parser wraps RHS in Paren; we gate the RHS evaluation here so
        // the right operand never runs when the left already decides the result.
        bool isAnd = b.Op is "and" or "&&";
        Value left = await EvalNode(b.Left, ctx).ConfigureAwait(false);
        if (left.IsTruthy() != isAnd)
        {
            return isAnd ? Value.Boolean.False : Value.Boolean.True;
        }
        Value right = await EvalNode(b.Right, ctx).ConfigureAwait(false);
        return Value.Boolean.Of(right.IsTruthy());
    }

    private async ValueTask<Value> EvalGenericBinary(Binary b, EvalContext ctx)
    {
        Value l = await EvalNode(b.Left, ctx).ConfigureAwait(false);
        Value r = await EvalNode(b.Right, ctx).ConfigureAwait(false);
        if (!ops.BinaryOps.TryGetValue(b.Op, out ExprFunc? fn))
        {
            throw new FunctionException(b.Op);
        }
        return await fn([l, r], ctx).ConfigureAwait(false);
    }

    private async ValueTask<Value> EvalTernary(Ternary t, EvalContext ctx)
    {
        if (t.Op == "?")
        {
            Value cond = await EvalNode(t.A, ctx).ConfigureAwait(false);
            return cond.IsTruthy()
                ? await EvalNode(t.B, ctx).ConfigureAwait(false)
                : await EvalNode(t.C, ctx).ConfigureAwait(false);
        }
        Value a = await EvalNode(t.A, ctx).ConfigureAwait(false);
        Value b = await EvalNode(t.B, ctx).ConfigureAwait(false);
        Value c = await EvalNode(t.C, ctx).ConfigureAwait(false);
        if (!ops.TernaryOps.TryGetValue(t.Op, out ExprFunc? fn))
        {
            throw new FunctionException(t.Op);
        }
        return await fn([a, b, c], ctx).ConfigureAwait(false);
    }

    // ---------- call / lambda / function-def ----------

    private async ValueTask<Value> EvalCall(Call c, EvalContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        if (++_callDepth > EvaluationLimits.MaxCallDepth)
        {
            _callDepth--;
            throw new EvaluationException(
                $"evaluation call depth exceeds {EvaluationLimits.MaxCallDepth} (runaway recursion?)"
            );
        }
        try
        {
            if (IsLazyIf(c))
            {
                return await EvalLazyIf(c, ctx).ConfigureAwait(false);
            }
            return await InvokeCall(c, ctx).ConfigureAwait(false);
        }
        finally
        {
            _callDepth--;
        }
    }

    // Lazy `if(cond, t, f)` - only the selected branch is evaluated.
    private static bool IsLazyIf(Call c) =>
        c.Callee is Ident { Name: "if" } && c.Args.Length == 3;

    private async ValueTask<Value> EvalLazyIf(Call c, EvalContext ctx)
    {
        Value cond = await EvalNode(c.Args[0], ctx).ConfigureAwait(false);
        Node chosen = cond.IsTruthy() ? c.Args[1] : c.Args[2];
        return await EvalNode(chosen, ctx).ConfigureAwait(false);
    }

    private async ValueTask<Value> InvokeCall(Call c, EvalContext ctx)
    {
        Value callee = await EvalNode(c.Callee, ctx).ConfigureAwait(false);
        if (callee is not Value.Function fn)
        {
            string name = c.Callee is Ident id ? id.Name : "<expression>";
            throw new FunctionException(name, message: Messages.NotCallable(name));
        }
        ExpressionValidator.ValidateAllowedFunction(fn, ops.CallableImplementations);

        var args = new Value[c.Args.Length];
        for (int i = 0; i < c.Args.Length; i++)
        {
            args[i] = await EvalNode(c.Args[i], ctx).ConfigureAwait(false);
        }
        return await fn.Invoke(args, ctx).ConfigureAwait(false);
    }

    private Value EvalLambda(Lambda l, EvalContext ctx)
    {
        Scope captured = ctx.Scope;
        ImmutableArray<string> paramsArr = l.Params;
        Node body = l.Body;

        ExprFunc impl = async (args, _) =>
        {
            Scope child = captured.CreateChild();
            for (int i = 0; i < paramsArr.Length; i++)
            {
                child.SetLocal(paramsArr[i], i < args.Length ? args[i] : Value.Undefined.Instance);
            }
            var inner = new EvalContext(
                child,
                ctx.ErrorHandler,
                ctx.Resolver,
                ctx.CancellationToken
            );
            return await EvalNode(body, inner).ConfigureAwait(false);
        };

        // Evaluator instance is per-evaluation (Expression.EvaluateAsync allocates a
        // fresh one), and the async chain below runs serialised under await, so a
        // plain increment is safe - no Interlocked needed.
        string name = $"__lambda_{++_lambdaCounter}__";
        return new Value.Function(impl, name) { IsExpressionLambda = true };
    }

    private Value EvalFunctionDef(FunctionDef fd, EvalContext ctx)
    {
        Value fn = EvalLambda(new Lambda(fd.Params, fd.Body, fd.Span), ctx);
        ctx.Scope.Assign(fd.Name, fn);
        return fn;
    }

    // ---------- control flow ----------

    private async ValueTask<Value> EvalCase(Case k, EvalContext ctx)
    {
        // `case` with a subject matches by strict equality; without a subject
        // it picks the first truthy `when`.
        Value? subject = k.Subject is null
            ? null
            : await EvalNode(k.Subject, ctx).ConfigureAwait(false);

        foreach (CaseArm arm in k.Arms)
        {
            Value when = await EvalNode(arm.When, ctx).ConfigureAwait(false);
            bool matches = subject is null
                ? when.IsTruthy()
                : CorePreset.StrictEquals(subject, when);
            if (matches)
            {
                return await EvalNode(arm.Then, ctx).ConfigureAwait(false);
            }
        }

        return k.Else is null
            ? Value.Undefined.Instance
            : await EvalNode(k.Else, ctx).ConfigureAwait(false);
    }

    private async ValueTask<Value> EvalSequence(Sequence s, EvalContext ctx)
    {
        Value last = Value.Undefined.Instance;
        foreach (Node stmt in s.Statements)
        {
            last = await EvalNode(stmt, ctx).ConfigureAwait(false);
        }
        return last;
    }

    // ---------- array / object literals with spread ----------

    private async ValueTask<Value> EvalArrayLit(ArrayLit a, EvalContext ctx)
    {
        ImmutableArray<Value>.Builder builder = ImmutableArray.CreateBuilder<Value>(
            a.Elements.Length
        );
        foreach (ArrayEntry entry in a.Elements)
        {
            await AppendArrayEntry(builder, entry, ctx).ConfigureAwait(false);
        }
        return new Value.Array(builder.ToImmutable());
    }

    private async ValueTask AppendArrayEntry(
        ImmutableArray<Value>.Builder builder,
        ArrayEntry entry,
        EvalContext ctx
    )
    {
        switch (entry)
        {
            case ArrayElement e:
                builder.Add(await EvalNode(e.Node, ctx).ConfigureAwait(false));
                return;
            case ArraySpread sp:
                Value v = await EvalNode(sp.Argument, ctx).ConfigureAwait(false);
                if (v is not Value.Array arr)
                {
                    throw new EvaluationException(
                        "spread target in array literal must be an array"
                    );
                }
                builder.AddRange(arr.Items);
                return;
        }
    }

    private async ValueTask<Value> EvalObjectLit(ObjectLit o, EvalContext ctx)
    {
        var dict = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (ObjectEntry entry in o.Properties)
        {
            await MergeObjectEntry(dict, entry, ctx).ConfigureAwait(false);
        }
        return Value.Object.From(dict);
    }

    private async ValueTask MergeObjectEntry(
        Dictionary<string, Value> dict,
        ObjectEntry entry,
        EvalContext ctx
    )
    {
        switch (entry)
        {
            case ObjectProperty p:
                dict[p.Key] = await EvalNode(p.Value, ctx).ConfigureAwait(false);
                return;
            case ObjectSpread sp:
                Value v = await EvalNode(sp.Argument, ctx).ConfigureAwait(false);
                if (v is not Value.Object obj)
                {
                    throw new EvaluationException(
                        "spread target in object literal must be an object"
                    );
                }
                foreach (KeyValuePair<string, Value> kv in obj.Props)
                {
                    dict[kv.Key] = kv.Value;
                }
                return;
        }
    }

    // ---------- post-processing ----------

    private static Value NormalizeNegativeZero(Value v) =>
        v is Value.Number { V: 0 } n && double.IsNegative(n.V) ? Value.Number.Of(0) : v;
}
