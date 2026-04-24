using Expreszo.Ast;

namespace Expreszo.LanguageServer.Tests;

public class AstLocatorTests
{
    [Test]
    public async Task Locates_ident_inside_call()
    {
        var parser = new Parser();
        Expression expr = parser.Parse("sum(xs)");
        Node root = expr.Root;

        // Offset 0 sits on the `sum` Ident. Since `sum` is the Callee of the
        // Call expression, Locate should descend into the Call and land on
        // the Ident node.
        LocateResult located = AstLocator.Locate(root, 0);

        await Assert.That(located.Found).IsTrue();
        await Assert.That(located.Deepest).IsTypeOf<Ident>();
        await Assert.That(((Ident)located.Deepest!).Name).IsEqualTo("sum");
    }

    [Test]
    public async Task Returns_none_for_offsets_outside_root_span()
    {
        var parser = new Parser();
        Expression expr = parser.Parse("1 + 2");
        Node root = expr.Root;

        LocateResult located = AstLocator.Locate(root, 999);

        await Assert.That(located.Found).IsFalse();
    }

    [Test]
    public async Task Chain_contains_all_ancestors_outermost_first()
    {
        var parser = new Parser();
        Expression expr = parser.Parse("1 + 2 * 3");
        Node root = expr.Root;

        // Offset on `3` - deepest should be a NumberLit, chain should include
        // the outer Binary `+` and the inner Binary `*`.
        LocateResult located = AstLocator.Locate(root, 8);

        await Assert.That(located.Found).IsTrue();
        await Assert.That(located.Deepest).IsTypeOf<NumberLit>();
        await Assert.That(located.Chain.Length).IsGreaterThan(1);
        await Assert.That(located.Chain[0]).IsSameReferenceAs(root);
    }
}
