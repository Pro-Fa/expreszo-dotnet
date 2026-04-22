using System.Globalization;
using System.Text;

namespace Expreszo.Ast.Visitors;

/// <summary>
/// Reconstructs a source-text-like rendering of the AST. Preserves
/// <see cref="Paren"/> nodes as explicit parentheses - matches the TS
/// library's IEXPR-preserving behaviour so simplified expressions round-trip
/// predictably.
/// </summary>
internal sealed class ToStringVisitor
{
    public static string Run(Node root)
    {
        var sb = new StringBuilder();
        Emit(root, sb);

        return sb.ToString();
    }

    private static void Emit(Node node, StringBuilder sb)
    {
        switch (node)
        {
            case NumberLit n:
                sb.Append(n.Value.ToString("R", CultureInfo.InvariantCulture));
                break;
            case StringLit s:
                sb.Append('"').Append(EscapeString(s.Value)).Append('"');
                break;
            case BoolLit b:
                sb.Append(b.Value ? "true" : "false");
                break;
            case NullLit:
                sb.Append("null");
                break;
            case UndefinedLit:
                sb.Append("undefined");
                break;
            case RawLit r:
                sb.Append(r.Value);
                break;
            case Ident id:
                sb.Append(id.Name);
                break;
            case NameRef nr:
                sb.Append(nr.Name);
                break;
            case Member m:
                Emit(m.Object, sb);
                sb.Append('.').Append(m.Property);
                break;
            case Paren p:
                sb.Append('(');
                Emit(p.Inner, sb);
                sb.Append(')');
                break;
            case Unary u:
                EmitUnary(u, sb);
                break;
            case Binary b:
                EmitBinary(b, sb);
                break;
            case Ternary t:
                sb.Append('(');
                Emit(t.A, sb);
                sb.Append(' ').Append(t.Op).Append(' ');
                Emit(t.B, sb);
                sb.Append(" : ");
                Emit(t.C, sb);
                sb.Append(')');
                break;
            case Call c:
                Emit(c.Callee, sb);
                sb.Append('(');
                for (int i = 0; i < c.Args.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    Emit(c.Args[i], sb);
                }
                sb.Append(')');
                break;
            case Lambda l:
                sb.Append('(');
                if (l.Params.Length == 1)
                {
                    sb.Append(l.Params[0]);
                }
                else
                {
                    sb.Append('(');
                    sb.Append(string.Join(", ", l.Params));
                    sb.Append(')');
                }
                sb.Append(" => ");
                Emit(l.Body, sb);
                sb.Append(')');
                break;
            case FunctionDef fd:
                sb.Append('(').Append(fd.Name).Append('(');
                sb.Append(string.Join(", ", fd.Params));
                sb.Append(") = ");
                Emit(fd.Body, sb);
                sb.Append(')');
                break;
            case ArrayLit a:
                sb.Append('[');
                for (int i = 0; i < a.Elements.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    switch (a.Elements[i])
                    {
                        case ArrayElement el:
                            Emit(el.Node, sb);
                            break;
                        case ArraySpread sp:
                            sb.Append("...");
                            Emit(sp.Argument, sb);
                            break;
                    }
                }
                sb.Append(']');
                break;
            case ObjectLit o:
                sb.Append('{');
                for (int i = 0; i < o.Properties.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    switch (o.Properties[i])
                    {
                        case ObjectProperty pr:
                            if (pr.Quoted)
                            {
                                sb.Append('"').Append(pr.Key).Append('"');
                            }
                            else
                            {
                                sb.Append(pr.Key);
                            }
                            sb.Append(": ");
                            Emit(pr.Value, sb);
                            break;
                        case ObjectSpread sp:
                            sb.Append("...");
                            Emit(sp.Argument, sb);
                            break;
                    }
                }
                sb.Append('}');
                break;
            case Sequence s:
                for (int i = 0; i < s.Statements.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(" ; ");
                    }

                    Emit(s.Statements[i], sb);
                }
                break;
            case Case k:
                sb.Append("case");
                if (k.Subject is not null)
                {
                    sb.Append(' ');
                    Emit(k.Subject, sb);
                }
                foreach (CaseArm arm in k.Arms)
                {
                    sb.Append(" when ");
                    Emit(arm.When, sb);
                    sb.Append(" then ");
                    Emit(arm.Then, sb);
                }
                if (k.Else is not null)
                {
                    sb.Append(" else ");
                    Emit(k.Else, sb);
                }
                sb.Append(" end");
                break;
        }
    }

    private static void EmitUnary(Unary u, StringBuilder sb)
    {
        if (u.Op == "!")
        {
            // Postfix
            sb.Append('(');
            Emit(u.Operand, sb);
            sb.Append("!)");
            return;
        }

        sb.Append('(').Append(u.Op);

        if (IsWordOp(u.Op))
        {
            sb.Append(' ');
        }

        Emit(u.Operand, sb);
        sb.Append(')');
    }

    private static void EmitBinary(Binary b, StringBuilder sb)
    {
        if (b.Op == "[")
        {
            Emit(b.Left, sb);
            sb.Append('[');
            Emit(b.Right, sb);
            sb.Append(']');
            return;
        }

        sb.Append('(');
        Emit(b.Left, sb);
        sb.Append(' ').Append(b.Op).Append(' ');
        Emit(b.Right, sb);
        sb.Append(')');
    }

    private static bool IsWordOp(string op)
    {
        // Name-form unary ops need whitespace before their operand (e.g. `not x`).
        if (string.IsNullOrEmpty(op))
        {
            return false;
        }

        return char.IsLetter(op[0]);
    }

    private static string EscapeString(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
