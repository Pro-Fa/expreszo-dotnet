using Expreszo.Ast;
using Expreszo.Parsing;

namespace Expreszo.Tests.Parsing;

public class PrattParserTests
{
    private static Node Parse(string expression) =>
        PrattParser.Parse(ParserConfig.Default, expression);

    // ---------- literals & identifiers ----------

    [Test]
    public async Task Parses_number_literal()
    {
        // Arrange

        // Act
        Node n = Parse("42");

        // Assert
        await Assert.That(n).IsTypeOf<NumberLit>();
        await Assert.That(((NumberLit)n).Value).IsEqualTo(42d);
    }

    [Test]
    public async Task Parses_string_literal()
    {
        // Arrange

        // Act
        Node n = Parse("\"hello\"");

        // Assert
        await Assert.That(n).IsTypeOf<StringLit>();
        await Assert.That(((StringLit)n).Value).IsEqualTo("hello");
    }

    [Test]
    public async Task Parses_boolean_literal()
    {
        // Arrange

        // Act
        Node n = Parse("true");

        // Assert
        await Assert.That(n).IsTypeOf<BoolLit>();
        await Assert.That(((BoolLit)n).Value).IsTrue();
    }

    [Test]
    public async Task Parses_null_literal()
    {
        // Arrange

        // Act
        Node n = Parse("null");

        // Assert
        await Assert.That(n).IsTypeOf<NullLit>();
    }

    [Test]
    public async Task Parses_undefined_as_UndefinedLit()
    {
        // Arrange

        // Act
        Node n = Parse("undefined");

        // Assert
        await Assert.That(n).IsTypeOf<UndefinedLit>();
    }

    [Test]
    public async Task Parses_identifier()
    {
        // Arrange

        // Act
        Node n = Parse("x");

        // Assert
        await Assert.That(n).IsTypeOf<Ident>();
        await Assert.That(((Ident)n).Name).IsEqualTo("x");
    }

    // ---------- arithmetic precedence ----------

    [Test]
    public async Task Respects_addsub_vs_muldiv_precedence()
    {
        // Arrange

        // Act
        // 1 + 2 * 3 should parse as 1 + (2 * 3)
        var n = (Binary)Parse("1 + 2 * 3");

        // Assert
        await Assert.That(n.Op).IsEqualTo("+");
        await Assert.That(n.Left).IsTypeOf<NumberLit>();
        await Assert.That(n.Right).IsTypeOf<Binary>();
        await Assert.That(((Binary)n.Right).Op).IsEqualTo("*");
    }

    [Test]
    public async Task Addsub_is_left_associative()
    {
        // Arrange

        // Act
        // 1 - 2 - 3 parses as (1 - 2) - 3
        var n = (Binary)Parse("1 - 2 - 3");

        // Assert
        await Assert.That(n.Op).IsEqualTo("-");
        await Assert.That(n.Left).IsTypeOf<Binary>();
        await Assert.That(((Binary)n.Left).Op).IsEqualTo("-");
    }

    [Test]
    public async Task Exponent_is_right_associative()
    {
        // Arrange

        // Act
        // 2 ^ 3 ^ 2 parses as 2 ^ (3 ^ 2)
        var n = (Binary)Parse("2 ^ 3 ^ 2");

        // Assert
        await Assert.That(n.Op).IsEqualTo("^");
        await Assert.That(n.Left).IsTypeOf<NumberLit>();
        await Assert.That(n.Right).IsTypeOf<Binary>();
        await Assert.That(((Binary)n.Right).Op).IsEqualTo("^");
    }

    // ---------- prefix & postfix unaries ----------

    [Test]
    public async Task Parses_prefix_minus()
    {
        // Arrange

        // Act
        Node n = Parse("-x");

        // Assert
        await Assert.That(n).IsTypeOf<Unary>();
        await Assert.That(((Unary)n).Op).IsEqualTo("-");
    }

    [Test]
    public async Task Parses_postfix_factorial()
    {
        // Arrange

        // Act
        Node n = Parse("5!");

        // Assert
        await Assert.That(n).IsTypeOf<Unary>();
        await Assert.That(((Unary)n).Op).IsEqualTo("!");
    }

    [Test]
    public async Task Parses_named_unary_operator_as_function_call()
    {
        // Arrange

        // Act
        Node n = Parse("sin(0)");

        // Assert
        await Assert.That(n).IsTypeOf<Unary>();
        await Assert.That(((Unary)n).Op).IsEqualTo("sin");
    }

    // ---------- comparisons ----------

    [Test]
    [Arguments("==")]
    [Arguments("!=")]
    [Arguments("<")]
    [Arguments(">")]
    [Arguments("<=")]
    [Arguments(">=")]
    [Arguments("in")]
    [Arguments("not in")]
    public async Task Parses_comparison_operators(string op)
    {
        // Arrange

        // Act
        var n = (Binary)Parse($"a {op} b");

        // Assert
        await Assert.That(n.Op).IsEqualTo(op);
    }

    // ---------- short-circuit operators wrap RHS in Paren ----------

    [Test]
    [Arguments("and")]
    [Arguments("&&")]
    [Arguments("or")]
    [Arguments("||")]
    public async Task Short_circuit_operators_wrap_RHS_in_Paren(string op)
    {
        // Arrange

        // Act
        var n = (Binary)Parse($"a {op} b");

        // Assert
        await Assert.That(n.Op).IsEqualTo(op);
        await Assert.That(n.Right).IsTypeOf<Paren>();
    }

    // ---------- ternary wraps branches in Paren ----------

    [Test]
    public async Task Ternary_wraps_both_branches_in_Paren()
    {
        // Arrange

        // Act
        var n = (Ternary)Parse("a ? b : c");

        // Assert
        await Assert.That(n.Op).IsEqualTo("?");
        await Assert.That(n.B).IsTypeOf<Paren>();
        await Assert.That(n.C).IsTypeOf<Paren>();
    }

    // ---------- assignment ----------

    [Test]
    public async Task Assignment_wraps_rhs_in_Paren_and_uses_NameRef_on_lhs()
    {
        // Arrange

        // Act
        var n = (Binary)Parse("x = 1");

        // Assert
        await Assert.That(n.Op).IsEqualTo("=");
        await Assert.That(n.Left).IsTypeOf<NameRef>();
        await Assert.That(n.Right).IsTypeOf<Paren>();
    }

    [Test]
    public async Task Function_definition_produces_FunctionDef_node()
    {
        // Arrange

        // Act
        Node n = Parse("f(x) = x * 2");

        // Assert
        await Assert.That(n).IsTypeOf<FunctionDef>();
        var fd = (FunctionDef)n;
        await Assert.That(fd.Name).IsEqualTo("f");
        await Assert.That(fd.Params.Length).IsEqualTo(1);
        await Assert.That(fd.Params[0]).IsEqualTo("x");
        await Assert.That(fd.Body).IsTypeOf<Paren>();
    }

    // ---------- arrow functions ----------

    [Test]
    public async Task Single_parameter_arrow_function_is_a_Lambda()
    {
        // Arrange

        // Act
        Node n = Parse("x => x + 1");

        // Assert
        await Assert.That(n).IsTypeOf<Lambda>();
        var l = (Lambda)n;
        await Assert.That(l.Params.Length).IsEqualTo(1);
        await Assert.That(l.Params[0]).IsEqualTo("x");
        await Assert.That(l.Body).IsTypeOf<Paren>();
    }

    [Test]
    public async Task Multi_parameter_arrow_function_is_a_Lambda()
    {
        // Arrange

        // Act
        Node n = Parse("(a, b) => a + b");

        // Assert
        await Assert.That(n).IsTypeOf<Lambda>();
        var l = (Lambda)n;
        await Assert.That(l.Params.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Zero_parameter_arrow_function_is_a_Lambda()
    {
        // Arrange

        // Act
        Node n = Parse("() => 42");

        // Assert
        await Assert.That(n).IsTypeOf<Lambda>();
        await Assert.That(((Lambda)n).Params.Length).IsEqualTo(0);
    }

    // ---------- grouping ----------

    [Test]
    public async Task Parentheses_are_transparent_for_single_expression()
    {
        // Arrange

        // Act
        // A single parenthesised expression is unwrapped (Paren is only emitted
        // at specific IEXPR-wrap sites, not around raw grouping).
        Node n = Parse("(1 + 2)");

        // Assert
        await Assert.That(n).IsTypeOf<Binary>();
    }

    [Test]
    public async Task Parentheses_override_precedence()
    {
        // Arrange

        // Act
        var n = (Binary)Parse("(1 + 2) * 3");

        // Assert
        await Assert.That(n.Op).IsEqualTo("*");
        await Assert.That(n.Left).IsTypeOf<Binary>();
        await Assert.That(((Binary)n.Left).Op).IsEqualTo("+");
    }

    // ---------- member access ----------

    [Test]
    public async Task Parses_dot_member_access()
    {
        // Arrange

        // Act
        var n = (Member)Parse("obj.prop");

        // Assert
        await Assert.That(n.Property).IsEqualTo("prop");
        await Assert.That(n.Object).IsTypeOf<Ident>();
    }

    [Test]
    public async Task Chains_member_access()
    {
        // Arrange

        // Act
        var n = (Member)Parse("a.b.c");

        // Assert
        await Assert.That(n.Property).IsEqualTo("c");
        var inner = (Member)n.Object;
        await Assert.That(inner.Property).IsEqualTo("b");
    }

    [Test]
    public async Task Bracket_access_is_Binary_with_op_bracket()
    {
        // Arrange

        // Act
        var n = (Binary)Parse("xs[0]");

        // Assert
        await Assert.That(n.Op).IsEqualTo("[");
        await Assert.That(n.Left).IsTypeOf<Ident>();
        await Assert.That(n.Right).IsTypeOf<NumberLit>();
    }

    // ---------- calls ----------

    [Test]
    public async Task Parses_zero_arg_function_call()
    {
        // Arrange

        // Act
        var n = (Call)Parse("f()");

        // Assert
        await Assert.That(n.Args.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Parses_multi_arg_function_call()
    {
        // Arrange

        // Act
        var n = (Call)Parse("max(a, b, c)");

        // Assert
        await Assert.That(n.Args.Length).IsEqualTo(3);
    }

    // ---------- array & object literals ----------

    [Test]
    public async Task Parses_empty_array_literal()
    {
        // Arrange

        // Act
        var n = (ArrayLit)Parse("[]");

        // Assert
        await Assert.That(n.Elements.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Parses_array_with_spread()
    {
        // Arrange

        // Act
        var n = (ArrayLit)Parse("[1, ...xs, 2]");

        // Assert
        await Assert.That(n.Elements.Length).IsEqualTo(3);
        await Assert.That(n.Elements[1]).IsTypeOf<ArraySpread>();
    }

    [Test]
    public async Task Parses_object_literal_with_identifier_and_string_keys()
    {
        // Arrange

        // Act
        var n = (ObjectLit)Parse("{ a: 1, \"b\": 2 }");

        // Assert
        await Assert.That(n.Properties.Length).IsEqualTo(2);
        var first = (ObjectProperty)n.Properties[0];
        var second = (ObjectProperty)n.Properties[1];
        await Assert.That(first.Key).IsEqualTo("a");
        await Assert.That(first.Quoted).IsFalse();
        await Assert.That(second.Key).IsEqualTo("b");
        await Assert.That(second.Quoted).IsTrue();
    }

    [Test]
    public async Task Parses_object_with_spread()
    {
        // Arrange

        // Act
        var n = (ObjectLit)Parse("{ ...base, x: 1 }");

        // Assert
        await Assert.That(n.Properties[0]).IsTypeOf<ObjectSpread>();
        await Assert.That(n.Properties[1]).IsTypeOf<ObjectProperty>();
    }

    // ---------- case expressions ----------

    [Test]
    public async Task Parses_case_with_subject_and_else()
    {
        // Arrange

        // Act
        var n = (Case)Parse("case x when 1 then \"one\" when 2 then \"two\" else \"other\" end");

        // Assert
        await Assert.That(n.Subject).IsNotNull();
        await Assert.That(n.Arms.Length).IsEqualTo(2);
        await Assert.That(n.Else).IsNotNull();
    }

    [Test]
    public async Task Parses_case_without_subject()
    {
        // Arrange

        // Act
        var n = (Case)Parse("case when a > 0 then 1 when a < 0 then -1 else 0 end");

        // Assert
        await Assert.That(n.Subject).IsNull();
        await Assert.That(n.Arms.Length).IsEqualTo(2);
    }

    // ---------- sequences ----------

    [Test]
    public async Task Semicolon_creates_nested_Paren_wrapped_Sequence()
    {
        // Arrange

        // Act
        // The TS Pratt parser recurses on every `;`, so `a; b; c` produces
        // Paren(Sequence([a, Paren(Sequence([b, c]))])) - two top-level
        // statements with the trailing statements bundled into a nested
        // Paren/Sequence. This preserves IEXPR positional parity.
        Node n = Parse("a = 1; b = 2; a + b");

        // Assert
        await Assert.That(n).IsTypeOf<Paren>();
        Node topSeq = ((Paren)n).Inner;
        await Assert.That(topSeq).IsTypeOf<Sequence>();
        await Assert.That(((Sequence)topSeq).Statements.Length).IsEqualTo(2);
        // Second statement is Paren(Sequence([b=2, a+b])).
        Node innerParen = ((Sequence)topSeq).Statements[1];
        await Assert.That(innerParen).IsTypeOf<Paren>();
        Node innerSeq = ((Paren)innerParen).Inner;
        await Assert.That(innerSeq).IsTypeOf<Sequence>();
        await Assert.That(((Sequence)innerSeq).Statements.Length).IsEqualTo(2);
    }

    // ---------- errors ----------

    [Test]
    public async Task Unexpected_eof_raises_ParseException()
    {
        // Arrange

        // Act
        Action act = () => Parse("1 +");

        // Assert
        await Assert.That(act).Throws<ParseException>();
    }

    [Test]
    public async Task Unclosed_paren_raises_ParseException()
    {
        // Arrange

        // Act
        Action act = () => Parse("(1 + 2");

        // Assert
        await Assert.That(act).Throws<ParseException>();
    }

    [Test]
    public async Task Missing_case_end_raises_ParseException()
    {
        // Arrange

        // Act
        Action act = () => Parse("case x when 1 then 2");

        // Assert
        await Assert.That(act).Throws<ParseException>();
    }

    [Test]
    public async Task Member_access_when_disabled_raises_AccessException()
    {
        // Arrange
        var disabled = new ParserConfig(
            ParserConfig.Default.Keywords,
            ParserConfig.Default.UnaryOps,
            ParserConfig.Default.BinaryOps,
            ParserConfig.Default.TernaryOps,
            ParserConfig.Default.NumericConstants,
            ParserConfig.Default.BuiltinLiterals,
            ParserConfig.Default.IsOperatorEnabled,
            allowMemberAccess: false
        );

        // Act
        Action act = () => PrattParser.Parse(disabled, "a.b");

        // Assert
        await Assert.That(act).Throws<AccessException>();
    }

    // ---------- spans ----------

    [Test]
    public async Task Span_covers_parsed_source()
    {
        // Arrange

        // Act
        Node n = Parse("  42  ");

        // Assert
        await Assert.That(n.Span.Start).IsEqualTo(2);
        await Assert.That(n.Span.End).IsEqualTo(4);
    }

    // ---------- integration ----------

    [Test]
    public async Task Parses_complex_expression_with_all_features()
    {
        // Arrange
        // mix of: call, arrow, comparison, ternary, array spread
        // (Avoiding .length - `length` tokenizes as TOP because it's a named
        // unary op; use count() for the array length instead.)
        string src = "count(map(xs, x => x * 2)) > 0 ? [1, ...rest] : null";

        // Act
        Node n = Parse(src);

        // Assert
        await Assert.That(n).IsNotNull();
    }
}
