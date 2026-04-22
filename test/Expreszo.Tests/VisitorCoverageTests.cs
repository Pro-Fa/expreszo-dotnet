using System.Text.Json;

namespace Expreszo.Tests;

/// <summary>
/// Phase 8 coverage fills: drives every AST-visitor branch through the
/// Expression.Simplify / Substitute / ToString / Symbols surface. Keeps
/// the tests expression-driven rather than hand-building AST so they stay
/// readable while still covering every node variant.
/// </summary>
public class VisitorCoverageTests
{
    private static readonly Parser Parser = new();

    // ---------- SimplifyVisitor ----------

    [Test]
    public async Task Simplify_folds_unary_with_literal_operand()
    {
        var e = Parser.Parse("-(1 + 2)").Simplify();
        await Assert.That(e.ToString()).IsEqualTo("-3");
    }

    [Test]
    public async Task Simplify_folds_ternary_with_constant_condition()
    {
        var e = Parser.Parse("true ? 42 : 99").Simplify();
        await Assert.That(e.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Simplify_recurses_into_call_and_keeps_call_node()
    {
        var e = Parser.Parse("max(1 + 2, 3 * 3)").Simplify();
        // Arguments fold but the Call remains (random-like impurity assumptions).
        await Assert.That(e.ToString()).IsEqualTo("max(3, 9)");
    }

    [Test]
    public async Task Simplify_recurses_into_array_and_object_literals()
    {
        var arr = Parser.Parse("[1 + 1, 2 + 2]").Simplify();
        await Assert.That(arr.ToString()).IsEqualTo("[2, 4]");

        var obj = Parser.Parse("{ a: 1 + 2, b: 3 * 4 }").Simplify();
        await Assert.That(obj.ToString()).IsEqualTo("{a: 3, b: 12}");
    }

    [Test]
    public async Task Simplify_recurses_into_case()
    {
        var e = Parser.Parse("case 1 + 1 when 2 then 4 * 4 else 0 end").Simplify();
        // subject folds to 2, arm condition folds to 2, arm body folds to 16
        await Assert.That(e.ToString()).Contains("2");
        await Assert.That(e.ToString()).Contains("16");
    }

    [Test]
    public async Task Simplify_leaves_assignment_untouched()
    {
        // Don't fold `=` — semantic side effect.
        var e = Parser.Parse("x = 1 + 2").Simplify();
        await Assert.That(e.ToString()).Contains("=");
    }

    [Test]
    public async Task Simplify_does_not_fold_when_operand_raises()
    {
        // Division by zero would throw at evaluation; simplify must preserve
        // the expression so the error surfaces at the real call site.
        var e = Parser.Parse("1 / 0").Simplify();
        await Assert.That(e.ToString()).Contains("/");
    }

    // ---------- SubstituteVisitor ----------

    [Test]
    public async Task Substitute_recurses_into_unary_and_ternary()
    {
        var e = Parser.Parse("-(x)").Substitute("x", "5").Evaluate();
        await Assert.That(((Value.Number)e).V).IsEqualTo(-5d);

        var e2 = Parser.Parse("x > 0 ? x : -x").Substitute("x", "-3").Evaluate();
        await Assert.That(((Value.Number)e2).V).IsEqualTo(3d);
    }

    [Test]
    public async Task Substitute_respects_lambda_parameter_shadowing()
    {
        // `x` in the lambda body is shadowed by the parameter, so substitution
        // should skip the body.
        var e = Parser.Parse("(x => x + 1)(5)");
        var replaced = e.Substitute("x", "999");
        await Assert.That(((Value.Number)replaced.Evaluate()).V).IsEqualTo(6d);
    }

    [Test]
    public async Task Substitute_recurses_into_array_object_case_sequence()
    {
        var arr = Parser.Parse("[x, x + 1]").Substitute("x", "10").Evaluate();
        var items = (Value.Array)arr;
        await Assert.That(((Value.Number)items.Items[0]).V).IsEqualTo(10d);
        await Assert.That(((Value.Number)items.Items[1]).V).IsEqualTo(11d);

        var obj = Parser.Parse("{ a: x }").Substitute("x", "42").Evaluate();
        await Assert.That(((Value.Number)((Value.Object)obj).Props["a"]).V).IsEqualTo(42d);

        var caseExpr = Parser.Parse("case x when 1 then \"one\" else \"other\" end").Substitute("x", "1").Evaluate();
        await Assert.That(((Value.String)caseExpr).V).IsEqualTo("one");
    }

    // ---------- SymbolsVisitor ----------

    [Test]
    public async Task Symbols_without_members_lists_each_prefix()
    {
        var e = Parser.Parse("obj.a.b + x");
        var syms = e.Symbols(withMembers: false);
        await Assert.That(syms).Contains("obj");
        await Assert.That(syms).Contains("x");
    }

    [Test]
    public async Task Symbols_with_members_groups_dotted_chains()
    {
        var e = Parser.Parse("obj.a.b + x");
        var syms = e.Symbols(withMembers: true);
        await Assert.That(syms).Contains("obj.a.b");
        await Assert.That(syms).Contains("x");
    }

    [Test]
    public async Task Symbols_walks_array_object_case_call_sequence()
    {
        var e = Parser.Parse("f([a, b], {c: d}, (case e when 1 then g else h end))");
        var syms = e.Symbols();
        foreach (var expected in new[] { "a", "b", "d", "e", "g", "h" })
        {
            await Assert.That(syms).Contains(expected);
        }
    }

    // ---------- ToStringVisitor ----------

    [Test]
    public async Task ToString_renders_array_and_object_literals()
    {
        await Assert.That(Parser.Parse("[1, 2, 3]").ToString()).IsEqualTo("[1, 2, 3]");
        await Assert.That(Parser.Parse("{a: 1, b: 2}").ToString()).IsEqualTo("{a: 1, b: 2}");
    }

    [Test]
    public async Task ToString_renders_lambda_and_function_def()
    {
        await Assert.That(Parser.Parse("x => x + 1").ToString()).Contains("=>");
        await Assert.That(Parser.Parse("f(x) = x * 2").ToString()).Contains("=");
    }

    [Test]
    public async Task ToString_renders_case_expression()
    {
        var s = Parser.Parse("case x when 1 then \"one\" else \"other\" end").ToString();
        await Assert.That(s).Contains("case");
        await Assert.That(s).Contains("when");
        await Assert.That(s).Contains("end");
    }

    [Test]
    public async Task ToString_renders_member_and_bracket()
    {
        await Assert.That(Parser.Parse("obj.prop").ToString()).IsEqualTo("obj.prop");
        await Assert.That(Parser.Parse("arr[0]").ToString()).IsEqualTo("arr[0]");
    }

    [Test]
    public async Task ToString_renders_postfix_factorial()
    {
        await Assert.That(Parser.Parse("5!").ToString()).IsEqualTo("(5!)");
    }

    [Test]
    public async Task ToString_escapes_special_characters_in_strings()
    {
        var s = Parser.Parse("\"line1\\nline2\"").ToString();
        await Assert.That(s).Contains("\\n");
    }

    // ---------- Additional evaluator coverage ----------

    [Test]
    public async Task Member_on_null_returns_Undefined()
    {
        using var doc = JsonDocument.Parse("""{"x":null}""");
        var result = Parser.Evaluate("x.prop", doc);
        await Assert.That(result).IsSameReferenceAs(Value.Undefined.Instance);
    }

    [Test]
    public async Task Assignment_to_non_identifier_throws()
    {
        // The parser accepts member-access assignment but the evaluator doesn't.
        // Use a pure number on the left-hand side to force the error.
        await Assert.That(() => Parser.Evaluate("5 = 10")).Throws<ParseException>();
    }

    [Test]
    public async Task Calling_non_function_throws()
    {
        await Assert.That(() => Parser.Evaluate("x = 5; x(1)")).Throws<FunctionException>();
    }

    [Test]
    public async Task Sequence_returns_last_statement_value()
    {
        var result = Parser.Evaluate("1; 2; 3");
        await Assert.That(((Value.Number)result).V).IsEqualTo(3d);
    }

    [Test]
    public async Task Case_with_no_matching_arm_and_no_else_returns_undefined()
    {
        var result = Parser.Evaluate("case 99 when 1 then \"x\" when 2 then \"y\" end");
        await Assert.That(result).IsSameReferenceAs(Value.Undefined.Instance);
    }

    [Test]
    public async Task Case_without_subject_uses_boolean_arms()
    {
        using var doc = JsonDocument.Parse("""{"x":5}""");
        var result = Parser.Evaluate(
            "case when x < 0 then \"neg\" when x > 0 then \"pos\" else \"zero\" end",
            doc);
        await Assert.That(((Value.String)result).V).IsEqualTo("pos");
    }

    // ---------- Extra operator coverage ----------

    [Test]
    [Arguments("2 + 3 | \"x\"", "5x")]     // concat coerces
    [Arguments("\"hi\" | \" world\"", "hi world")]
    public async Task Pipe_concatenation(string expr, string expected)
    {
        var result = Parser.Evaluate(expr);
        await Assert.That(((Value.String)result).V).IsEqualTo(expected);
    }

    [Test]
    public async Task Concat_of_two_arrays_produces_array()
    {
        var result = (Value.Array)Parser.Evaluate("[1, 2] | [3, 4]");
        await Assert.That(result.Items.Length).IsEqualTo(4);
    }

    [Test]
    [Arguments("5 as \"boolean\"", true)]
    [Arguments("0 as \"boolean\"", false)]
    public async Task As_operator_casts_to_boolean(string expr, bool expected)
    {
        var result = Parser.Evaluate(expr);
        await Assert.That(((Value.Boolean)result).V).IsEqualTo(expected);
    }

    [Test]
    [Arguments("\"42\" as \"number\"", 42d)]
    [Arguments("3.7 as \"int\"", 4d)]
    public async Task As_operator_casts_numerics(string expr, double expected)
    {
        var result = Parser.Evaluate(expr);
        await Assert.That(((Value.Number)result).V).IsEqualTo(expected);
    }
}
