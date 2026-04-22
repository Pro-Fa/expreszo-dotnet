using System.Collections.Immutable;

namespace Expreszo.Ast.Visitors;

/// <summary>
/// Returns a new AST where every <see cref="Ident"/> whose name matches the
/// target variable is replaced by the given replacement subtree.
/// </summary>
internal sealed class SubstituteVisitor
{
    private readonly string _variable;
    private readonly Node _replacement;

    public SubstituteVisitor(string variable, Node replacement)
    {
        _variable = variable;
        _replacement = replacement;
    }

    public Node Substitute(Node node) => node switch
    {
        Ident id when id.Name == _variable => _replacement,
        Ident or NumberLit or StringLit or BoolLit or NullLit or UndefinedLit or RawLit or NameRef => node,
        Paren p => new Paren(Substitute(p.Inner), p.Span),
        Unary u => new Unary(u.Op, Substitute(u.Operand), u.Span),
        Binary b => new Binary(b.Op, Substitute(b.Left), Substitute(b.Right), b.Span),
        Ternary t => new Ternary(t.Op, Substitute(t.A), Substitute(t.B), Substitute(t.C), t.Span),
        Member m => new Member(Substitute(m.Object), m.Property, m.Span),
        Call c => new Call(Substitute(c.Callee), [.. c.Args.Select(Substitute)], c.Span),
        Lambda l when l.Params.Contains(_variable) => l, // variable is shadowed by parameter
        Lambda l => new Lambda(l.Params, Substitute(l.Body), l.Span),
        FunctionDef fd when fd.Params.Contains(_variable) => fd,
        FunctionDef fd => new FunctionDef(fd.Name, fd.Params, Substitute(fd.Body), fd.Span),
        ArrayLit a => SubstituteArray(a),
        ObjectLit o => SubstituteObject(o),
        Sequence s => new Sequence([.. s.Statements.Select(Substitute)], s.Span),
        Case k => SubstituteCase(k),
        _ => node,
    };

    private ArrayLit SubstituteArray(ArrayLit a) => new(
        a.Elements.Select<ArrayEntry, ArrayEntry>(e => e switch
        {
            ArrayElement el => new ArrayElement(Substitute(el.Node), el.Span),
            ArraySpread sp => new ArraySpread(Substitute(sp.Argument), sp.Span),
            _ => e,
        }).ToImmutableArray(),
        a.Span);

    private ObjectLit SubstituteObject(ObjectLit o) => new(
        o.Properties.Select<ObjectEntry, ObjectEntry>(p => p switch
        {
            ObjectProperty pr => new ObjectProperty(pr.Key, Substitute(pr.Value), pr.Quoted, pr.Span),
            ObjectSpread sp => new ObjectSpread(Substitute(sp.Argument), sp.Span),
            _ => p,
        }).ToImmutableArray(),
        o.Span);

    private Case SubstituteCase(Case k) => new(
        k.Subject is null ? null : Substitute(k.Subject),
        k.Arms.Select(arm => new CaseArm(Substitute(arm.When), Substitute(arm.Then))).ToImmutableArray(),
        k.Else is null ? null : Substitute(k.Else),
        k.Span);
}
