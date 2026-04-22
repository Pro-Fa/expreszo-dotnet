using System.Text;

namespace Expreszo.Ast.Visitors;

/// <summary>
/// Collects every symbol referenced by the AST. With <c>withMembers</c> enabled,
/// <c>a.b.c</c> is collected as a single dotted name rather than three separate
/// references.
/// </summary>
internal sealed class SymbolsVisitor
{
    private readonly bool _withMembers;

    public SymbolsVisitor(bool withMembers) => _withMembers = withMembers;

    public IReadOnlyList<string> Collect(Node root)
    {
        var symbols = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Visit(root, symbols, seen);
        return symbols;
    }

    private void Visit(Node node, List<string> symbols, HashSet<string> seen)
    {
        switch (node)
        {
            case Ident id:
                Add(id.Name, symbols, seen);
                break;
            case Member m when _withMembers && TryGetMemberChain(m, out var chain):
                Add(chain, symbols, seen);
                break;
            case Member m:
                Visit(m.Object, symbols, seen);
                break;
            case Paren p:
                Visit(p.Inner, symbols, seen);
                break;
            case Unary u:
                Visit(u.Operand, symbols, seen);
                break;
            case Binary b:
                Visit(b.Left, symbols, seen);
                Visit(b.Right, symbols, seen);
                break;
            case Ternary t:
                Visit(t.A, symbols, seen);
                Visit(t.B, symbols, seen);
                Visit(t.C, symbols, seen);
                break;
            case Call c:
                Visit(c.Callee, symbols, seen);
                foreach (var arg in c.Args) Visit(arg, symbols, seen);
                break;
            case Lambda l:
                Visit(l.Body, symbols, seen);
                break;
            case FunctionDef fd:
                Visit(fd.Body, symbols, seen);
                break;
            case ArrayLit a:
                foreach (var e in a.Elements)
                {
                    Visit(
                        e switch
                        {
                            ArrayElement el => el.Node,
                            ArraySpread sp => sp.Argument,
                            _ => throw new NotSupportedException(),
                        },
                        symbols, seen);
                }
                break;
            case ObjectLit o:
                foreach (var p in o.Properties)
                {
                    Visit(
                        p switch
                        {
                            ObjectProperty pr => pr.Value,
                            ObjectSpread sp => sp.Argument,
                            _ => throw new NotSupportedException(),
                        },
                        symbols, seen);
                }
                break;
            case Sequence s:
                foreach (var stmt in s.Statements) Visit(stmt, symbols, seen);
                break;
            case Case k:
                if (k.Subject is not null) Visit(k.Subject, symbols, seen);
                foreach (var arm in k.Arms)
                {
                    Visit(arm.When, symbols, seen);
                    Visit(arm.Then, symbols, seen);
                }
                if (k.Else is not null) Visit(k.Else, symbols, seen);
                break;
        }
    }

    private static bool TryGetMemberChain(Member m, out string chain)
    {
        var parts = new Stack<string>();
        parts.Push(m.Property);
        Node cursor = m.Object;
        while (cursor is Member inner)
        {
            parts.Push(inner.Property);
            cursor = inner.Object;
        }
        if (cursor is not Ident head)
        {
            chain = string.Empty;
            return false;
        }
        parts.Push(head.Name);
        var sb = new StringBuilder();
        var first = true;
        while (parts.Count > 0)
        {
            if (!first) sb.Append('.');
            sb.Append(parts.Pop());
            first = false;
        }
        chain = sb.ToString();
        return true;
    }

    private static void Add(string symbol, List<string> symbols, HashSet<string> seen)
    {
        if (seen.Add(symbol)) symbols.Add(symbol);
    }
}
