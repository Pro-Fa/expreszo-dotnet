using System.Collections.Immutable;
using Expreszo.Ast;

namespace Expreszo.LanguageServer;

/// <summary>
/// Literal-driven, flow-insensitive type inference over an AST. One
/// post-order walk annotates every node with a <see cref="ValueKind"/>.
/// Free identifiers, user-function return values, and anything behind an
/// operator whose result shape depends on runtime data all stay
/// <see cref="ValueKind.Unknown"/>; the validator reads this pass and only
/// flags problems where both (or the sole) operands are known.
/// </summary>
internal sealed class TypeInference
{
    private readonly Dictionary<Node, ValueKind> _kinds = [];

    private TypeInference() { }

    public IReadOnlyDictionary<Node, ValueKind> Kinds => _kinds;

    public ValueKind KindOf(Node node) =>
        _kinds.TryGetValue(node, out ValueKind k) ? k : ValueKind.Unknown;

    public static TypeInference Run(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var inference = new TypeInference();
        inference.Visit(root);
        return inference;
    }

    private ValueKind Visit(Node node)
    {
        ValueKind kind = node switch
        {
            NumberLit => ValueKind.Number,
            StringLit => ValueKind.String,
            BoolLit => ValueKind.Boolean,
            NullLit => ValueKind.Null,
            UndefinedLit => ValueKind.Undefined,
            RawLit r => KindOfValue(r.Value),
            ArrayLit a => VisitArray(a),
            ObjectLit o => VisitObject(o),
            Ident => ValueKind.Unknown,
            NameRef => ValueKind.Unknown,
            Member m => VisitMember(m),
            Unary u => VisitUnary(u),
            Binary b => VisitBinary(b),
            Ternary t => VisitTernary(t),
            Call c => VisitCall(c),
            Lambda l => VisitLambda(l),
            FunctionDef fd => VisitFunctionDef(fd),
            Case cs => VisitCase(cs),
            Sequence s => VisitSequence(s),
            Paren p => VisitParen(p),
            _ => ValueKind.Unknown,
        };

        _kinds[node] = kind;
        return kind;
    }

    private ValueKind VisitArray(ArrayLit a)
    {
        foreach (ArrayEntry entry in a.Elements)
        {
            Visit(entry switch
            {
                ArrayElement e => e.Node,
                ArraySpread s => s.Argument,
                _ => throw new NotSupportedException(),
            });
        }
        return ValueKind.Array;
    }

    private ValueKind VisitObject(ObjectLit o)
    {
        foreach (ObjectEntry entry in o.Properties)
        {
            Visit(entry switch
            {
                ObjectProperty p => p.Value,
                ObjectSpread s => s.Argument,
                _ => throw new NotSupportedException(),
            });
        }
        return ValueKind.Object;
    }

    private ValueKind VisitMember(Member m)
    {
        Visit(m.Object);
        return ValueKind.Unknown;
    }

    private ValueKind VisitUnary(Unary u)
    {
        Visit(u.Operand);
        return u.Op switch
        {
            "-" or "+" or "!" => ValueKind.Number,
            "not" => ValueKind.Boolean,
            _ when IsMathUnary(u.Op) => ValueKind.Number,
            _ => ValueKind.Unknown,
        };
    }

    private ValueKind VisitBinary(Binary b)
    {
        ValueKind left = Visit(b.Left);
        ValueKind right = Visit(b.Right);

        return b.Op switch
        {
            "+" or "-" or "*" or "/" or "%" or "^" => ValueKind.Number,
            "|" => (left, right) switch
            {
                (ValueKind.Array, ValueKind.Array) => ValueKind.Array,
                _ => ValueKind.String,
            },
            "==" or "!=" or "<" or "<=" or ">" or ">=" or "in" or "not in" => ValueKind.Boolean,
            // and/or/&&/|| short-circuit returns one of the operands rather
            // than a boolean (matching JS semantics); declaring Boolean here
            // would cause false positives in dead-branch analysis.
            "and" or "or" or "&&" or "||" => ValueKind.Unknown,
            "??" => ValueKind.Unknown,
            "=" => right,
            "[" => ValueKind.Unknown,
            "as" => b.Right switch
            {
                StringLit s => s.Value switch
                {
                    "number" or "int" or "integer" => ValueKind.Number,
                    "boolean" => ValueKind.Boolean,
                    _ => ValueKind.Unknown,
                },
                _ => ValueKind.Unknown,
            },
            _ => ValueKind.Unknown,
        };
    }

    private ValueKind VisitTernary(Ternary t)
    {
        Visit(t.A);
        ValueKind b = Visit(t.B);
        ValueKind c = Visit(t.C);
        return b == c ? b : ValueKind.Unknown;
    }

    private ValueKind VisitCall(Call c)
    {
        Visit(c.Callee);
        foreach (Node arg in c.Args)
        {
            Visit(arg);
        }

        if (c.Callee is Ident id && BuiltinMetadata.TryGet(id.Name, out BuiltinEntry? entry))
        {
            return entry.ReturnKind;
        }

        return ValueKind.Unknown;
    }

    private ValueKind VisitLambda(Lambda l)
    {
        Visit(l.Body);
        return ValueKind.Function;
    }

    private ValueKind VisitFunctionDef(FunctionDef fd)
    {
        Visit(fd.Body);
        return ValueKind.Function;
    }

    private ValueKind VisitCase(Case cs)
    {
        if (cs.Subject is not null)
        {
            Visit(cs.Subject);
        }

        ValueKind? common = null;
        foreach (CaseArm arm in cs.Arms)
        {
            Visit(arm.When);
            ValueKind thenKind = Visit(arm.Then);
            common = common is null ? thenKind : (common == thenKind ? common : ValueKind.Unknown);
        }

        if (cs.Else is not null)
        {
            ValueKind elseKind = Visit(cs.Else);
            common = common is null ? elseKind : (common == elseKind ? common : ValueKind.Unknown);
        }

        return common ?? ValueKind.Unknown;
    }

    private ValueKind VisitSequence(Sequence s)
    {
        ValueKind last = ValueKind.Unknown;
        foreach (Node stmt in s.Statements)
        {
            last = Visit(stmt);
        }
        return last;
    }

    private ValueKind VisitParen(Paren p) => Visit(p.Inner);

    private static bool IsMathUnary(string op) =>
        op switch
        {
            "abs" or "ceil" or "floor" or "round" or "sign" or "sqrt" or "cbrt" or "trunc"
            or "exp" or "expm1" or "log" or "ln" or "log1p" or "log2" or "log10" or "lg"
            or "sin" or "cos" or "tan" or "asin" or "acos" or "atan"
            or "sinh" or "cosh" or "tanh" or "asinh" or "acosh" or "atanh"
            or "length" => true,
            _ => false,
        };

    private static ValueKind KindOfValue(Value v) =>
        v switch
        {
            Value.Number => ValueKind.Number,
            Value.String => ValueKind.String,
            Value.Boolean => ValueKind.Boolean,
            Value.Null => ValueKind.Null,
            Value.Undefined => ValueKind.Undefined,
            Value.Array => ValueKind.Array,
            Value.Object => ValueKind.Object,
            Value.Function => ValueKind.Function,
            _ => ValueKind.Unknown,
        };
}
