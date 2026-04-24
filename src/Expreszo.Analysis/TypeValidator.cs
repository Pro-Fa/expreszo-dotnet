using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Errors;

namespace Expreszo.Analysis;

/// <summary>
/// Consumes the output of <see cref="TypeInference"/> and emits
/// <see cref="SemanticException"/> diagnostics for literal-driven problems
/// the evaluator would hit at runtime. Conservative: never fires when an
/// operand's kind is <see cref="ValueKind.Unknown"/>, so idiomatic dynamic
/// code stays quiet.
/// </summary>
public static class TypeValidator
{
    private static readonly ImmutableHashSet<string> ValidCastTargets = [
        "number", "int", "integer", "boolean",
    ];

    /// <summary>
    /// Walks <paramref name="root"/> and returns every semantic-error
    /// diagnostic found, using <paramref name="inference"/> for per-node
    /// type kinds.
    /// </summary>
    /// <param name="root">AST to validate.</param>
    /// <param name="inference">Result of <see cref="TypeInference.Run"/> over the same root.</param>
    /// <param name="source">Optional source text used to populate <see cref="ErrorContext.Expression"/>.</param>
    /// <exception cref="ArgumentNullException">Either required argument is <c>null</c>.</exception>
    public static ImmutableArray<ExpressionException> Validate(
        Node root,
        TypeInference inference,
        string? source = null
    )
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(inference);

        var diagnostics = ImmutableArray.CreateBuilder<ExpressionException>();

        Ast.Ast.Walk(root, node =>
        {
            switch (node)
            {
                case Binary b:
                    CheckBinary(b, inference, source, diagnostics);
                    break;
                case Call c:
                    CheckCall(c, inference, source, diagnostics);
                    break;
            }
        });

        return diagnostics.ToImmutable();
    }

    private static void CheckBinary(
        Binary b,
        TypeInference inference,
        string? source,
        ImmutableArray<ExpressionException>.Builder diagnostics
    )
    {
        switch (b.Op)
        {
            case "+":
                CheckNumericBinary(b, inference, source, diagnostics);
                break;
            case "/":
                CheckDivide(b, source, diagnostics);
                break;
            case "as":
                CheckCast(b, source, diagnostics);
                break;
        }
    }

    private static void CheckNumericBinary(
        Binary b,
        TypeInference inference,
        string? source,
        ImmutableArray<ExpressionException>.Builder diagnostics
    )
    {
        ValueKind left = inference.KindOf(b.Left);
        ValueKind right = inference.KindOf(b.Right);

        if (IsDefinitelyNonNumeric(left) || IsDefinitelyNonNumeric(right))
        {
            diagnostics.Add(new SemanticException(
                $"operator '+' requires numeric operands (left: {left}, right: {right})",
                Context(b.Span, source)
            ));
        }
    }

    private static void CheckDivide(
        Binary b,
        string? source,
        ImmutableArray<ExpressionException>.Builder diagnostics
    )
    {
        if (b.Right is NumberLit n && n.Value == 0d)
        {
            diagnostics.Add(new SemanticException(
                "division by zero",
                Context(b.Span, source)
            ));
        }
    }

    private static void CheckCast(
        Binary b,
        string? source,
        ImmutableArray<ExpressionException>.Builder diagnostics
    )
    {
        if (b.Right is StringLit s && !ValidCastTargets.Contains(s.Value))
        {
            string valid = string.Join(", ", ValidCastTargets.OrderBy(x => x));
            diagnostics.Add(new SemanticException(
                $"unsupported cast target \"{s.Value}\"; expected one of: {valid}",
                Context(b.Right.Span, source)
            ));
        }
    }

    private static void CheckCall(
        Call c,
        TypeInference inference,
        string? source,
        ImmutableArray<ExpressionException>.Builder diagnostics
    )
    {
        if (c.Callee is not Ident id)
        {
            return;
        }

        if (!BuiltinMetadata.TryGet(id.Name, out BuiltinEntry? entry) ||
            entry.Kind != BuiltinKind.Function)
        {
            return;
        }

        int argCount = c.Args.Length;

        if (argCount < entry.MinArity)
        {
            diagnostics.Add(new SemanticException(
                ArityMessage(id.Name, entry.MinArity, entry.MaxArity, argCount, tooFew: true),
                Context(c.Span, source, functionName: id.Name)
            ));
        }
        else if (argCount > entry.MaxArity)
        {
            diagnostics.Add(new SemanticException(
                ArityMessage(id.Name, entry.MinArity, entry.MaxArity, argCount, tooFew: false),
                Context(c.Span, source, functionName: id.Name)
            ));
        }

        // Argument kind checks — skip if we didn't supply kinds for this builtin.
        if (entry.ParameterKinds.IsDefaultOrEmpty)
        {
            return;
        }

        int checkCount = Math.Min(argCount, entry.ParameterKinds.Length);
        for (int i = 0; i < checkCount; i++)
        {
            ValueKind expected = entry.ParameterKinds[i];
            if (expected == ValueKind.Unknown)
            {
                continue;
            }

            ValueKind actual = inference.KindOf(c.Args[i]);
            if (actual == ValueKind.Unknown || actual == ValueKind.Undefined)
            {
                continue;
            }

            if (actual != expected)
            {
                diagnostics.Add(new SemanticException(
                    $"argument {i + 1} of '{id.Name}' expects {expected}, got {actual}",
                    Context(c.Args[i].Span, source, functionName: id.Name, argumentIndex: i)
                ));
            }
        }

        // Dead-branch heuristic for the isXxx family: when the sole
        // argument's kind is known and mismatches the predicate, the call
        // can only evaluate to a constant.
        if (argCount == 1 && IsTypeCheckPredicate(id.Name, out ValueKind targetKind))
        {
            ValueKind actual = inference.KindOf(c.Args[0]);
            if (actual != ValueKind.Unknown &&
                actual != ValueKind.Undefined &&
                targetKind != ValueKind.Undefined)
            {
                bool certain = actual != ValueKind.Unknown;
                if (certain && actual != targetKind)
                {
                    diagnostics.Add(new SemanticException(
                        $"'{id.Name}' is always false for an argument of kind {actual}",
                        Context(c.Span, source, functionName: id.Name)
                    ));
                }
            }
        }
    }

    private static bool IsDefinitelyNonNumeric(ValueKind k) =>
        k is ValueKind.String
            or ValueKind.Array
            or ValueKind.Object
            or ValueKind.Function;

    private static string ArityMessage(string name, int min, int max, int got, bool tooFew)
    {
        string expected = min == max
            ? $"{min}"
            : max == int.MaxValue
                ? $"at least {min}"
                : $"{min}..{max}";
        string word = tooFew ? "few" : "many";
        return $"too {word} arguments to '{name}' (expected {expected}, got {got})";
    }

    private static bool IsTypeCheckPredicate(string name, out ValueKind kind)
    {
        kind = name switch
        {
            "isArray" => ValueKind.Array,
            "isObject" => ValueKind.Object,
            "isNumber" => ValueKind.Number,
            "isString" => ValueKind.String,
            "isBoolean" => ValueKind.Boolean,
            "isNull" => ValueKind.Null,
            "isUndefined" => ValueKind.Undefined,
            "isFunction" => ValueKind.Function,
            _ => ValueKind.Unknown,
        };
        return kind != ValueKind.Unknown;
    }

    private static ErrorContext Context(
        TextSpan span,
        string? source,
        string? functionName = null,
        int? argumentIndex = null
    ) => new()
    {
        Span = span,
        Expression = source,
        FunctionName = functionName,
        ArgumentIndex = argumentIndex,
    };
}
