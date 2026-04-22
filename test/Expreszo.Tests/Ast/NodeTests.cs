using System.Collections.Immutable;
using Expreszo.Ast;

namespace Expreszo.Tests.Ast;

public class NodeTests
{
    [Test]
    public async Task Literal_nodes_expose_their_values()
    {
        // Arrange

        // Act & Assert
        await Assert.That(new NumberLit(42, Node.NoSpan).Value).IsEqualTo(42d);
        await Assert.That(new StringLit("hi", Node.NoSpan).Value).IsEqualTo("hi");
        await Assert.That(new BoolLit(true, Node.NoSpan).Value).IsTrue();
    }

    [Test]
    public async Task Records_compare_structurally()
    {
        // Arrange
        var a = new Binary(
            "+",
            new NumberLit(1, Node.NoSpan),
            new NumberLit(2, Node.NoSpan),
            Node.NoSpan
        );
        var b = new Binary(
            "+",
            new NumberLit(1, Node.NoSpan),
            new NumberLit(2, Node.NoSpan),
            Node.NoSpan
        );

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task ArrayLit_equality_is_sequence_based()
    {
        // Arrange
        var a = new ArrayLit(
            ImmutableArray.Create<ArrayEntry>(
                new ArrayElement(new NumberLit(1, Node.NoSpan), Node.NoSpan),
                new ArrayElement(new NumberLit(2, Node.NoSpan), Node.NoSpan)
            ),
            Node.NoSpan
        );
        var b = new ArrayLit(
            ImmutableArray.Create<ArrayEntry>(
                new ArrayElement(new NumberLit(1, Node.NoSpan), Node.NoSpan),
                new ArrayElement(new NumberLit(2, Node.NoSpan), Node.NoSpan)
            ),
            Node.NoSpan
        );

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task ObjectLit_equality_respects_order()
    {
        // Arrange
        var a = new ObjectLit(
            ImmutableArray.Create<ObjectEntry>(
                new ObjectProperty("x", new NumberLit(1, Node.NoSpan), false, Node.NoSpan),
                new ObjectProperty("y", new NumberLit(2, Node.NoSpan), false, Node.NoSpan)
            ),
            Node.NoSpan
        );
        var b = new ObjectLit(
            ImmutableArray.Create<ObjectEntry>(
                new ObjectProperty("y", new NumberLit(2, Node.NoSpan), false, Node.NoSpan),
                new ObjectProperty("x", new NumberLit(1, Node.NoSpan), false, Node.NoSpan)
            ),
            Node.NoSpan
        );

        // Act

        // Assert
        // In an ObjectLit the declared order matters for source faithfulness.
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task Lambda_equality_considers_params_and_body()
    {
        // Arrange
        var a = new Lambda(
            ImmutableArray.Create("x", "y"),
            new Ident("x", Node.NoSpan),
            Node.NoSpan
        );
        var b = new Lambda(
            ImmutableArray.Create("x", "y"),
            new Ident("x", Node.NoSpan),
            Node.NoSpan
        );
        var c = new Lambda(
            ImmutableArray.Create("y", "x"),
            new Ident("x", Node.NoSpan),
            Node.NoSpan
        );

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a).IsNotEqualTo(c);
    }

    [Test]
    public async Task Span_is_stored_verbatim()
    {
        // Arrange
        var span = new Expreszo.Errors.TextSpan(3, 7);

        // Act
        var n = new NumberLit(5, span);

        // Assert
        await Assert.That(n.Span).IsEqualTo(span);
    }
}

public class NodeVisitorTests
{
    private sealed class CountingVisitor : NodeVisitor<int>
    {
        public int Ids;
        public int Numbers;
        public int Binaries;

        public override int VisitIdent(Ident node)
        {
            Ids++;
            return 0;
        }

        public override int VisitNumberLit(NumberLit node)
        {
            Numbers++;
            return 0;
        }

        public override int VisitBinary(Binary node)
        {
            Binaries++;
            Visit(node.Left);
            Visit(node.Right);
            return 0;
        }
    }

    [Test]
    public async Task Visit_dispatches_on_node_type()
    {
        // Arrange
        var visitor = new CountingVisitor();
        Node root = new Binary(
            "+",
            new Ident("x", Node.NoSpan),
            new NumberLit(1, Node.NoSpan),
            Node.NoSpan
        );

        // Act
        visitor.Visit(root);

        // Assert
        await Assert.That(visitor.Binaries).IsEqualTo(1);
        await Assert.That(visitor.Ids).IsEqualTo(1);
        await Assert.That(visitor.Numbers).IsEqualTo(1);
    }

    [Test]
    public async Task Visit_throws_for_unsupported_node_types_by_default()
    {
        // Arrange
        var visitor = new CountingVisitor();
        Node node = new Unary("-", new NumberLit(1, Node.NoSpan), Node.NoSpan);

        // Act
        Action act = () => visitor.Visit(node);

        // Assert
        await Assert.That(act).Throws<NotImplementedException>();
    }
}

public class AstWalkTests
{
    [Test]
    public async Task Walk_visits_every_node_post_order()
    {
        // Arrange
        // (x + 1) * 2
        Node expr = new Binary(
            "*",
            new Paren(
                new Binary(
                    "+",
                    new Ident("x", Node.NoSpan),
                    new NumberLit(1, Node.NoSpan),
                    Node.NoSpan
                ),
                Node.NoSpan
            ),
            new NumberLit(2, Node.NoSpan),
            Node.NoSpan
        );
        var visited = new List<string>();

        // Act
        Expreszo.Ast.Ast.Walk(expr, node => visited.Add(node.GetType().Name));

        // Assert
        // Post-order: Ident, NumberLit(1), Binary(+), Paren, NumberLit(2), Binary(*)
        string[] expected = ["Ident", "NumberLit", "Binary", "Paren", "NumberLit", "Binary"];
        await Assert.That(visited.Count).IsEqualTo(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            await Assert.That(visited[i]).IsEqualTo(expected[i]);
        }
    }

    [Test]
    public async Task Walk_descends_into_array_and_object_entries()
    {
        // Arrange
        var arrayLit = new ArrayLit(
            ImmutableArray.Create<ArrayEntry>(
                new ArrayElement(new NumberLit(1, Node.NoSpan), Node.NoSpan),
                new ArraySpread(new Ident("xs", Node.NoSpan), Node.NoSpan)
            ),
            Node.NoSpan
        );
        var visited = new List<Type>();

        // Act
        Expreszo.Ast.Ast.Walk(arrayLit, n => visited.Add(n.GetType()));

        // Assert
        await Assert.That(visited).Contains(typeof(NumberLit));
        await Assert.That(visited).Contains(typeof(Ident));
        await Assert.That(visited).Contains(typeof(ArrayLit));
    }

    [Test]
    public async Task Walk_descends_into_case_arms_and_else()
    {
        // Arrange
        Node c = new Case(
            Subject: new Ident("n", Node.NoSpan),
            Arms: ImmutableArray.Create(
                new CaseArm(new NumberLit(1, Node.NoSpan), new StringLit("one", Node.NoSpan)),
                new CaseArm(new NumberLit(2, Node.NoSpan), new StringLit("two", Node.NoSpan))
            ),
            Else: new StringLit("other", Node.NoSpan),
            Span: Node.NoSpan
        );
        int count = 0;

        // Act
        Expreszo.Ast.Ast.Walk(c, _ => count++);

        // Assert
        // Ident (subject) + 2 arms * 2 nodes + else + self = 1 + 4 + 1 + 1 = 7
        await Assert.That(count).IsEqualTo(7);
    }
}
