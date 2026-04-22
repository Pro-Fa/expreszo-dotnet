namespace Expreszo.Ast;

/// <summary>
/// Visitor pattern over the AST. Each method is called when the corresponding
/// node type is visited. For typed traversal, inherit from
/// <see cref="NodeVisitor{T}"/>; for a side-effect-only walk, use
/// <see cref="Ast.Walk"/>.
/// </summary>
public interface INodeVisitor<out T>
{
    T Visit(Node node);

    T VisitNumberLit(NumberLit node);
    T VisitStringLit(StringLit node);
    T VisitBoolLit(BoolLit node);
    T VisitNullLit(NullLit node);
    T VisitUndefinedLit(UndefinedLit node);
    T VisitRawLit(RawLit node);

    T VisitArrayLit(ArrayLit node);
    T VisitObjectLit(ObjectLit node);

    T VisitIdent(Ident node);
    T VisitNameRef(NameRef node);
    T VisitMember(Member node);

    T VisitUnary(Unary node);
    T VisitBinary(Binary node);
    T VisitTernary(Ternary node);

    T VisitCall(Call node);
    T VisitLambda(Lambda node);
    T VisitFunctionDef(FunctionDef node);

    T VisitCase(Case node);
    T VisitSequence(Sequence node);
    T VisitParen(Paren node);
}

/// <summary>
/// Base class for typed visitors. Dispatches <see cref="Visit(Node)"/> to the
/// kind-specific method. Subclasses override only the methods they care about
/// - the rest can be left abstract or routed through
/// <see cref="VisitDefault"/>.
/// </summary>
public abstract class NodeVisitor<T> : INodeVisitor<T>
{
    public virtual T Visit(Node node) =>
        node switch
        {
            NumberLit n => VisitNumberLit(n),
            StringLit n => VisitStringLit(n),
            BoolLit n => VisitBoolLit(n),
            NullLit n => VisitNullLit(n),
            UndefinedLit n => VisitUndefinedLit(n),
            RawLit n => VisitRawLit(n),
            ArrayLit n => VisitArrayLit(n),
            ObjectLit n => VisitObjectLit(n),
            Ident n => VisitIdent(n),
            NameRef n => VisitNameRef(n),
            Member n => VisitMember(n),
            Unary n => VisitUnary(n),
            Binary n => VisitBinary(n),
            Ternary n => VisitTernary(n),
            Call n => VisitCall(n),
            Lambda n => VisitLambda(n),
            FunctionDef n => VisitFunctionDef(n),
            Case n => VisitCase(n),
            Sequence n => VisitSequence(n),
            Paren n => VisitParen(n),
            _ => throw new NotSupportedException($"Unknown AST node: {node.GetType().Name}"),
        };

    /// <summary>Fallback used by all <c>VisitXxx</c> methods by default. Override for catch-all behaviour.</summary>
    protected virtual T VisitDefault(Node node) =>
        throw new NotImplementedException(
            $"Visitor {GetType().Name} has no override for {node.GetType().Name}"
        );

    public virtual T VisitNumberLit(NumberLit node) => VisitDefault(node);

    public virtual T VisitStringLit(StringLit node) => VisitDefault(node);

    public virtual T VisitBoolLit(BoolLit node) => VisitDefault(node);

    public virtual T VisitNullLit(NullLit node) => VisitDefault(node);

    public virtual T VisitUndefinedLit(UndefinedLit node) => VisitDefault(node);

    public virtual T VisitRawLit(RawLit node) => VisitDefault(node);

    public virtual T VisitArrayLit(ArrayLit node) => VisitDefault(node);

    public virtual T VisitObjectLit(ObjectLit node) => VisitDefault(node);

    public virtual T VisitIdent(Ident node) => VisitDefault(node);

    public virtual T VisitNameRef(NameRef node) => VisitDefault(node);

    public virtual T VisitMember(Member node) => VisitDefault(node);

    public virtual T VisitUnary(Unary node) => VisitDefault(node);

    public virtual T VisitBinary(Binary node) => VisitDefault(node);

    public virtual T VisitTernary(Ternary node) => VisitDefault(node);

    public virtual T VisitCall(Call node) => VisitDefault(node);

    public virtual T VisitLambda(Lambda node) => VisitDefault(node);

    public virtual T VisitFunctionDef(FunctionDef node) => VisitDefault(node);

    public virtual T VisitCase(Case node) => VisitDefault(node);

    public virtual T VisitSequence(Sequence node) => VisitDefault(node);

    public virtual T VisitParen(Paren node) => VisitDefault(node);
}

/// <summary>
/// Helpers over AST nodes - currently just <see cref="Walk"/>, a post-order
/// traversal that invokes the callback on every node (children first, then the
/// node itself). Matches the TS <c>walk()</c> helper in
/// <c>src/ast/visitor.ts</c>.
/// </summary>
public static class Ast
{
    /// <summary>Post-order traversal of the AST.</summary>
    /// <param name="root">Root node to walk.</param>
    /// <param name="visit">Callback invoked for each node after its children.</param>
    public static void Walk(Node root, Action<Node> visit)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(visit);
        WalkInternal(root, visit);
    }

    private static void WalkInternal(Node node, Action<Node> visit)
    {
        switch (node)
        {
            case ArrayLit a:
                foreach (ArrayEntry entry in a.Elements)
                {
                    WalkInternal(
                        entry switch
                        {
                            ArrayElement e => e.Node,
                            ArraySpread s => s.Argument,
                            _ => throw new NotSupportedException(),
                        },
                        visit
                    );
                }
                break;
            case ObjectLit o:
                foreach (ObjectEntry entry in o.Properties)
                {
                    WalkInternal(
                        entry switch
                        {
                            ObjectProperty p => p.Value,
                            ObjectSpread s => s.Argument,
                            _ => throw new NotSupportedException(),
                        },
                        visit
                    );
                }
                break;
            case Member m:
                WalkInternal(m.Object, visit);
                break;
            case Unary u:
                WalkInternal(u.Operand, visit);
                break;
            case Binary b:
                WalkInternal(b.Left, visit);
                WalkInternal(b.Right, visit);
                break;
            case Ternary t:
                WalkInternal(t.A, visit);
                WalkInternal(t.B, visit);
                WalkInternal(t.C, visit);
                break;
            case Call c:
                WalkInternal(c.Callee, visit);
                foreach (Node arg in c.Args)
                {
                    WalkInternal(arg, visit);
                }
                break;
            case Lambda l:
                WalkInternal(l.Body, visit);
                break;
            case FunctionDef fd:
                WalkInternal(fd.Body, visit);
                break;
            case Case k:
                if (k.Subject is not null)
                {
                    WalkInternal(k.Subject, visit);
                }
                foreach (CaseArm arm in k.Arms)
                {
                    WalkInternal(arm.When, visit);
                    WalkInternal(arm.Then, visit);
                }
                if (k.Else is not null)
                {
                    WalkInternal(k.Else, visit);
                }
                break;
            case Sequence s:
                foreach (Node stmt in s.Statements)
                {
                    WalkInternal(stmt, visit);
                }
                break;
            case Paren p:
                WalkInternal(p.Inner, visit);
                break;
        }
        visit(node);
    }
}
