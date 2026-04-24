using Expreszo.Ast;

namespace Expreszo.Tests;

public class TryParseTests
{
    [Test]
    public async Task Clean_source_produces_no_errors()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("1 + 2");

        await Assert.That(result.HasErrors).IsFalse();
        await Assert.That(result.Errors.IsDefaultOrEmpty).IsTrue();
    }

    [Test]
    public async Task Parsed_expression_can_still_be_evaluated()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("1 + 2");
        Value value = result.Expression.Evaluate();

        await Assert.That(value is Value.Number n && n.V == 3d).IsTrue();
    }

    [Test]
    public async Task Single_statement_syntax_error_surfaces_one_error()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("1 +");

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Errors.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Broken_middle_statement_does_not_break_its_neighbours()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("a = 1; b = 1 +; c = 3");

        await Assert.That(result.Errors.Length).IsEqualTo(1);

        // The healthy statements should be reachable from the AST. Any kind
        // of walkable shape that yields "a" and "c" as assignment targets
        // confirms recovery worked.
        var names = new List<string>();
        global::Expreszo.Ast.Ast.Walk(result.Expression.Root, node =>
        {
            if (node is NameRef nr)
            {
                names.Add(nr.Name);
            }
        });

        await Assert.That(names).Contains("a");
        await Assert.That(names).Contains("c");
    }

    [Test]
    public async Task Empty_source_produces_empty_expression_with_no_errors()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("");

        await Assert.That(result.HasErrors).IsFalse();
        await Assert.That(result.Expression.Root).IsTypeOf<UndefinedLit>();
    }

    [Test]
    public async Task Whitespace_only_source_produces_empty_expression_with_no_errors()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("   \n\t  ");

        await Assert.That(result.HasErrors).IsFalse();
        await Assert.That(result.Expression.Root).IsTypeOf<UndefinedLit>();
    }

    [Test]
    public async Task Spans_on_recovered_nodes_reference_absolute_offsets()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("a = 1; b = 2");

        // Collect every Ident/NameRef to check they carry absolute offsets.
        var seen = new List<(string Name, int Start, int End)>();
        global::Expreszo.Ast.Ast.Walk(result.Expression.Root, node =>
        {
            if (node is NameRef nr)
            {
                seen.Add((nr.Name, nr.Span.Start, nr.Span.End));
            }
        });

        // "a" is at absolute offset 0..1, "b" at 7..8.
        await Assert.That(seen).Contains(("a", 0, 1));
        await Assert.That(seen).Contains(("b", 7, 8));
    }

    [Test]
    public async Task Multi_line_source_preserves_error_positions()
    {
        var parser = new Parser();

        ParseResult result = parser.TryParse("a = 1;\nb = 1 +;\nc = 3");

        await Assert.That(result.Errors.Length).IsEqualTo(1);
        // The error for `b = 1 +` should report line 2 (1-based).
        await Assert.That(result.Errors[0].Position?.Line).IsEqualTo(2);
    }
}
