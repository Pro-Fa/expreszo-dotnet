using System.Collections.Immutable;
using Expreszo.Ast;

namespace Expreszo.Analysis;

/// <summary>
/// Locates the deepest <see cref="Node"/> whose <see cref="Node.Span"/>
/// contains a given source offset. Backs hover, completion-context, and
/// goto / rename features.
/// </summary>
public static class AstLocator
{
    /// <summary>
    /// Returns the narrowest node containing <paramref name="offset"/>, along
    /// with its ancestor chain (outermost first). Returns <see cref="LocateResult.None"/>
    /// if the offset lies outside <paramref name="root"/>'s span.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <c>null</c>.</exception>
    public static LocateResult Locate(Node root, int offset)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (offset < root.Span.Start || offset > root.Span.End)
        {
            return LocateResult.None;
        }

        List<Node> chain = [];
        Descend(root, offset, chain);

        if (chain.Count == 0)
        {
            return LocateResult.None;
        }

        Node deepest = chain[^1];
        return new LocateResult(deepest, [.. chain]);
    }

    private static void Descend(Node node, int offset, List<Node> chain)
    {
        chain.Add(node);

        foreach (Node child in Children(node))
        {
            if (offset >= child.Span.Start && offset <= child.Span.End)
            {
                Descend(child, offset, chain);
                return;
            }
        }
    }

    private static IEnumerable<Node> Children(Node node) =>
        node switch
        {
            ArrayLit a => a.Elements.SelectMany(EntryChildren),
            ObjectLit o => o.Properties.SelectMany(EntryChildren),
            Member m => [m.Object],
            Unary u => [u.Operand],
            Binary b => [b.Left, b.Right],
            Ternary t => [t.A, t.B, t.C],
            Call c => [c.Callee, .. c.Args],
            Lambda l => [l.Body],
            FunctionDef f => [f.Body],
            Case cs => CaseChildren(cs),
            Sequence s => s.Statements,
            Paren p => [p.Inner],
            _ => [],
        };

    private static IEnumerable<Node> EntryChildren(ArrayEntry entry) =>
        entry switch
        {
            ArrayElement e => [e.Node],
            ArraySpread s => [s.Argument],
            _ => [],
        };

    private static IEnumerable<Node> EntryChildren(ObjectEntry entry) =>
        entry switch
        {
            ObjectProperty p => [p.Value],
            ObjectSpread s => [s.Argument],
            _ => [],
        };

    private static IEnumerable<Node> CaseChildren(Case c)
    {
        if (c.Subject is not null)
        {
            yield return c.Subject;
        }

        foreach (CaseArm arm in c.Arms)
        {
            yield return arm.When;
            yield return arm.Then;
        }

        if (c.Else is not null)
        {
            yield return c.Else;
        }
    }
}

/// <summary>
/// Result of an <see cref="AstLocator.Locate"/> call. <see cref="Chain"/>
/// is ordered outermost-first (index 0 is the root), and <see cref="Deepest"/>
/// is the last element of the chain.
/// </summary>
/// <param name="Deepest">The narrowest node containing the queried offset, or <c>null</c> when no match.</param>
/// <param name="Chain">Ancestor chain from root to deepest. Empty when no match.</param>
public readonly record struct LocateResult(Node? Deepest, ImmutableArray<Node> Chain)
{
    /// <summary>Sentinel value representing "offset didn't match any node".</summary>
    public static LocateResult None { get; } = new(null, []);

    /// <summary>True when <see cref="Deepest"/> is populated.</summary>
    public bool Found => Deepest is not null;
}
