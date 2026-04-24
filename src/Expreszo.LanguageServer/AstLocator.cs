using System.Collections.Immutable;
using Expreszo.Ast;

namespace Expreszo.LanguageServer;

/// <summary>
/// Locates the deepest <see cref="Node"/> whose <see cref="Node.Span"/>
/// contains a given source offset. Backs hover, completion-context, and
/// future goto / rename features.
/// </summary>
internal static class AstLocator
{
    /// <summary>
    /// Returns the narrowest node containing <paramref name="offset"/>, along
    /// with its ancestor chain (outermost first). Returns an empty result if
    /// the offset lies outside the root span.
    /// </summary>
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

/// <summary>Result of an <see cref="AstLocator.Locate"/> call.</summary>
internal readonly record struct LocateResult(Node? Deepest, ImmutableArray<Node> Chain)
{
    public static LocateResult None { get; } = new(null, []);

    public bool Found => Deepest is not null;
}
