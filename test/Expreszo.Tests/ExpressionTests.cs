using System.Text.Json;

namespace Expreszo.Tests;

public class ExpressionTests
{
    private static readonly Parser Parser = new();

    // ---------- arithmetic ----------

    [Test]
    [Arguments("1 + 2", 3d)]
    [Arguments("10 - 3", 7d)]
    [Arguments("2 * 5", 10d)]
    [Arguments("10 / 4", 2.5)]
    [Arguments("10 % 3", 1d)]
    [Arguments("2 ^ 10", 1024d)]
    [Arguments("-5 + 3", -2d)]
    [Arguments("1 + 2 * 3", 7d)]
    [Arguments("(1 + 2) * 3", 9d)]
    [Arguments("2 ^ 3 ^ 2", 512d)] // right-associative: 2^(3^2) = 2^9
    public async Task Evaluates_arithmetic(string expr, double expected)
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate(expr);

        // Assert
        await Assert.That(result).IsTypeOf<Value.Number>();
        await Assert.That(((Value.Number)result).V).IsEqualTo(expected);
    }

    [Test]
    public async Task Division_by_zero_throws()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("1 / 0");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---------- comparisons ----------

    [Test]
    [Arguments("1 == 1", true)]
    [Arguments("1 == 2", false)]
    [Arguments("1 != 2", true)]
    [Arguments("1 < 2", true)]
    [Arguments("2 > 1", true)]
    [Arguments("2 <= 2", true)]
    [Arguments("2 >= 3", false)]
    public async Task Evaluates_comparisons(string expr, bool expected)
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate(expr);

        // Assert
        await Assert.That(result).IsEqualTo((Value)Value.Boolean.Of(expected));
    }

    // ---------- short-circuit ----------

    [Test]
    public async Task And_short_circuits_on_falsy_left()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("false and (1 / 0)");

        // Assert
        await Assert.That(result).IsEqualTo((Value)Value.Boolean.False);
    }

    [Test]
    public async Task Or_short_circuits_on_truthy_left()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("true or (1 / 0)");

        // Assert
        await Assert.That(result).IsEqualTo((Value)Value.Boolean.True);
    }

    // ---------- ternary ----------

    [Test]
    public async Task Ternary_picks_true_branch()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("1 < 2 ? 42 : 99");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    [Test]
    public async Task Ternary_picks_false_branch_and_skips_true()
    {
        // Arrange

        // Act
        // Division by zero on the true branch would throw, but we never take it.
        Value result = Parser.Evaluate("false ? (1 / 0) : 42");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    // ---------- null coalesce ----------

    [Test]
    [Arguments("null ?? 5", 5d)]
    [Arguments("undefined ?? 5", 5d)]
    [Arguments("1 ?? 5", 1d)]
    public async Task Null_coalesce_picks_first_non_nullish(string expr, double expected)
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate(expr);

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(expected);
    }

    // ---------- variables via JsonDocument ----------

    [Test]
    public async Task Reads_variable_from_json_document()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"x\":10,\"y\":32}");

        // Act
        Value result = Parser.Evaluate("x + y", doc);

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    [Test]
    public async Task Undefined_variable_throws_VariableException()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("nope + 1");

        // Assert
        await Assert.That(act).Throws<VariableException>();
    }

    [Test]
    public async Task Custom_resolver_is_consulted_before_error()
    {
        // Arrange
        VariableResolver resolver = name =>
            name == "answer"
                ? new VariableResolveResult.Bound(Value.Number.Of(42))
                : VariableResolveResult.NotResolved;

        // Act
        Value result = Parser.Evaluate("answer", values: null, resolver: resolver);

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    // ---------- numeric constants ----------

    [Test]
    public async Task Resolves_PI_as_numeric_constant()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("PI");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(Math.PI);
    }

    // ---------- assignment + sequences ----------

    [Test]
    public async Task Assignment_binds_in_scope_for_subsequent_statements()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("x = 10; y = 32; x + y");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    [Test]
    public async Task Function_def_produces_callable()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("f(x) = x * 2; f(5)");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(10d);
    }

    // ---------- lambdas ----------

    [Test]
    public async Task Lambda_value_is_a_Function()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("x => x + 1");

        // Assert
        await Assert.That(result).IsTypeOf<Value.Function>();
    }

    [Test]
    public async Task Lambda_can_be_invoked_in_a_call()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("g = x => x + 1; g(41)");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    // ---------- array / object literals ----------

    [Test]
    public async Task Array_literal_evaluates_to_Value_Array()
    {
        // Arrange

        // Act
        var result = (Value.Array)Parser.Evaluate("[1, 2, 3]");

        // Assert
        await Assert.That(result.Items.Length).IsEqualTo(3);
        await Assert.That(((Value.Number)result.Items[0]).V).IsEqualTo(1d);
    }

    [Test]
    public async Task Array_with_spread_inlines_values()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"rest\":[2,3,4]}");

        // Act
        var result = (Value.Array)Parser.Evaluate("[1, ...rest, 5]", doc);

        // Assert
        await Assert.That(result.Items.Length).IsEqualTo(5);
        await Assert.That(((Value.Number)result.Items[3]).V).IsEqualTo(4d);
    }

    [Test]
    public async Task Object_literal_evaluates_to_Value_Object()
    {
        // Arrange

        // Act
        var result = (Value.Object)Parser.Evaluate("{ a: 1, b: 2 }");

        // Assert
        await Assert.That(result.Props.Count).IsEqualTo(2);
        await Assert.That(((Value.Number)result.Props["a"]).V).IsEqualTo(1d);
    }

    [Test]
    public async Task Object_spread_inlines_properties()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"base\":{\"x\":1,\"y\":2}}");

        // Act
        var result = (Value.Object)Parser.Evaluate("{ ...base, z: 3 }", doc);

        // Assert
        await Assert.That(result.Props.Count).IsEqualTo(3);
    }

    // ---------- member / bracket access ----------

    [Test]
    public async Task Dot_member_access_reads_property()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"obj\":{\"name\":\"alice\"}}");

        // Act
        Value result = Parser.Evaluate("obj.name", doc);

        // Assert
        await Assert.That(((Value.String)result).V).IsEqualTo("alice");
    }

    [Test]
    public async Task Missing_member_returns_Undefined()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"obj\":{}}");

        // Act
        Value result = Parser.Evaluate("obj.missing", doc);

        // Assert
        await Assert.That(result).IsSameReferenceAs(Value.Undefined.Instance);
    }

    [Test]
    public async Task Bracket_access_reads_array_index()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"xs\":[10,20,30]}");

        // Act
        Value result = Parser.Evaluate("xs[1]", doc);

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(20d);
    }

    // ---------- case expressions ----------

    [Test]
    public async Task Case_with_subject_returns_matching_arm()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"x\":2}");

        // Act
        Value result = Parser.Evaluate(
            "case x when 1 then \"one\" when 2 then \"two\" else \"other\" end",
            doc
        );

        // Assert
        await Assert.That(((Value.String)result).V).IsEqualTo("two");
    }

    [Test]
    public async Task Case_falls_through_to_else()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"x\":99}");

        // Act
        Value result = Parser.Evaluate("case x when 1 then \"one\" else \"other\" end", doc);

        // Assert
        await Assert.That(((Value.String)result).V).IsEqualTo("other");
    }

    // ---------- simplify ----------

    [Test]
    public async Task Simplify_folds_constant_arithmetic()
    {
        // Arrange
        Expression expr = Parser.Parse("1 + 2 * 3");

        // Act
        Expression simplified = expr.Simplify();

        // Assert
        // Should fold to a single number literal 7
        await Assert.That(simplified.ToString()).IsEqualTo("7");
    }

    [Test]
    public async Task Simplify_with_values_inlines_variables()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"x\":10}");
        Expression expr = Parser.Parse("x + 5");

        // Act
        Expression simplified = expr.Simplify(doc);

        // Assert
        await Assert.That(simplified.ToString()).IsEqualTo("15");
    }

    // ---------- substitute ----------

    [Test]
    public async Task Substitute_replaces_variable_with_expression()
    {
        // Arrange
        Expression expr = Parser.Parse("x + 1");

        // Act
        Expression replaced = expr.Substitute("x", "5 * 2");
        Value result = replaced.Evaluate();

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(11d);
    }

    // ---------- symbols / variables ----------

    [Test]
    public async Task Symbols_lists_referenced_names()
    {
        // Arrange
        Expression expr = Parser.Parse("x + y + max(a, b)");

        // Act
        IReadOnlyList<string> symbols = expr.Symbols();

        // Assert
        await Assert.That(symbols).Contains("x");
        await Assert.That(symbols).Contains("y");
        await Assert.That(symbols).Contains("a");
        await Assert.That(symbols).Contains("b");
    }

    [Test]
    public async Task Variables_excludes_builtin_unary_operators_referenced_as_identifiers()
    {
        // Arrange
        // `sin` is a unary op so it shouldn't appear in Variables().
        Expression expr = Parser.Parse("sin(x)");

        // Act
        IReadOnlyList<string> vars = expr.Variables();

        // Assert
        await Assert.That(vars).Contains("x");
        await Assert.That(vars).DoesNotContain("sin");
    }

    // ---------- ToString ----------

    [Test]
    public async Task ToString_renders_arithmetic_with_parens()
    {
        // Arrange
        Expression expr = Parser.Parse("1 + 2 * 3");

        // Act
        string rendered = expr.ToString();

        // Assert
        await Assert.That(rendered).IsEqualTo("(1 + (2 * 3))");
    }

    [Test]
    public async Task ToString_renders_string_literal_with_quotes()
    {
        // Arrange
        Expression expr = Parser.Parse("\"hello\"");

        // Act
        string rendered = expr.ToString();

        // Assert
        await Assert.That(rendered).IsEqualTo("\"hello\"");
    }

    // ---------- async ----------

    [Test]
    public async Task Sync_Evaluate_of_sync_expression_works()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("1 + 2");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(3d);
    }

    [Test]
    public async Task EvaluateAsync_completes_synchronously_for_sync_expression()
    {
        // Arrange
        Expression expr = Parser.Parse("1 + 2");

        // Act
        ValueTask<Value> task = expr.EvaluateAsync();

        // Assert
        await Assert.That(task.IsCompletedSuccessfully).IsTrue();
        // Result is safe to access since we verified IsCompletedSuccessfully above.
        var result = (Value.Number)task.Result;
        await Assert.That(result.V).IsEqualTo(3d);
    }

    [Test]
    public async Task Cancellation_is_honoured_on_async_path()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Expression expr = Parser.Parse("1 + 2");

        // Act
        Func<Task> act = async () =>
        {
            await expr.EvaluateAsync(cancellationToken: cts.Token);
        };

        // Assert
        await Assert.That(act).Throws<OperationCanceledException>();
    }

    // ---------- accept visitor ----------

    [Test]
    public async Task Accept_runs_user_visitor_against_root()
    {
        // Arrange
        Expression expr = Parser.Parse("1 + 2");

        // Act
        int depth = expr.Accept(new DepthVisitor());

        // Assert
        await Assert.That(depth).IsGreaterThan(0);
    }

    private sealed class DepthVisitor : Expreszo.Ast.NodeVisitor<int>
    {
        protected override int VisitDefault(Expreszo.Ast.Node node) => 1;

        public override int VisitBinary(Expreszo.Ast.Binary node) =>
            1 + Math.Max(Visit(node.Left), Visit(node.Right));

        public override int VisitParen(Expreszo.Ast.Paren node) => Visit(node.Inner);
    }
}
