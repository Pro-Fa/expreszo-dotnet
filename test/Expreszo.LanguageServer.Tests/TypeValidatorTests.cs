using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Errors;

namespace Expreszo.LanguageServer.Tests;

public class TypeValidatorTests
{
    private static ImmutableArray<ExpressionException> Validate(string source)
    {
        Node root = new Parser().TryParse(source).Expression.Root;
        TypeInference inf = TypeInference.Run(root);
        return TypeValidator.Validate(root, inf, source);
    }

    [Test]
    public async Task Plus_on_string_literal_flags_a_semantic_error()
    {
        ImmutableArray<ExpressionException> errors = Validate("\"foo\" + 1");

        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0]).IsTypeOf<SemanticException>();
        await Assert.That(errors[0].Message).Contains("numeric operands");
    }

    [Test]
    public async Task Plus_on_unknown_variable_stays_silent()
    {
        ImmutableArray<ExpressionException> errors = Validate("x + 1");

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task As_with_unsupported_target_is_flagged()
    {
        ImmutableArray<ExpressionException> errors = Validate("x as \"bogus\"");

        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Message).Contains("unsupported cast target");
    }

    [Test]
    public async Task As_with_supported_target_is_silent()
    {
        ImmutableArray<ExpressionException> errors = Validate("x as \"number\"");

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Literal_divide_by_zero_is_flagged()
    {
        ImmutableArray<ExpressionException> errors = Validate("10 / 0");

        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Message).Contains("division by zero");
    }

    [Test]
    public async Task Divide_by_variable_is_silent()
    {
        ImmutableArray<ExpressionException> errors = Validate("10 / x");

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Builtin_arity_too_few_is_flagged()
    {
        ImmutableArray<ExpressionException> errors = Validate("clamp(1, 2)");

        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Message).Contains("too few arguments");
    }

    [Test]
    public async Task Builtin_arity_too_many_is_flagged()
    {
        ImmutableArray<ExpressionException> errors = Validate("clamp(1, 2, 3, 4)");

        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Message).Contains("too many arguments");
    }

    [Test]
    public async Task Builtin_wrong_arg_kind_is_flagged()
    {
        // sum expects Array, passing a literal Number should trip the check.
        ImmutableArray<ExpressionException> errors = Validate("sum(42)");

        await Assert.That(errors.Any(e => e.Message.Contains("expects Array"))).IsTrue();
    }

    [Test]
    public async Task Builtin_matching_arg_kind_is_silent()
    {
        ImmutableArray<ExpressionException> errors = Validate("sum([1, 2, 3])");

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Builtin_with_unknown_arg_is_silent()
    {
        // Variable `xs` is Unknown — validator must not fire on dynamic code.
        ImmutableArray<ExpressionException> errors = Validate("sum(xs)");

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task IsNumber_with_literal_number_is_silent()
    {
        ImmutableArray<ExpressionException> errors = Validate("isNumber(1)");

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task IsString_with_literal_number_is_flagged_as_dead_branch()
    {
        ImmutableArray<ExpressionException> errors = Validate("isString(1)");

        await Assert.That(errors.Any(e => e.Message.Contains("always false"))).IsTrue();
    }

    [Test]
    public async Task Variadic_builtins_accept_any_arg_count()
    {
        ImmutableArray<ExpressionException> errors = Validate("max(1, 2, 3, 4, 5)");

        await Assert.That(errors.Length).IsEqualTo(0);
    }
}
