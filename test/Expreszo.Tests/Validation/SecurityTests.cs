using System.Text.Json;
using Expreszo.Validation;

namespace Expreszo.Tests.Validation;

public class SecurityTests
{
    private static readonly Parser Parser = new();

    // ---------- Prototype pollution guards ----------

    [Test]
    public async Task Member_access_to___proto___is_blocked()
    {
        using var doc = JsonDocument.Parse("{\"obj\":{}}");
        await Assert.That(() => Parser.Evaluate("obj.__proto__", doc)).Throws<AccessException>();
    }

    [Test]
    public async Task Member_access_to_constructor_is_blocked()
    {
        using var doc = JsonDocument.Parse("{\"obj\":{}}");
        await Assert.That(() => Parser.Evaluate("obj.constructor", doc)).Throws<AccessException>();
    }

    [Test]
    public async Task Member_access_to_prototype_is_blocked()
    {
        using var doc = JsonDocument.Parse("{\"obj\":{}}");
        await Assert.That(() => Parser.Evaluate("obj.prototype", doc)).Throws<AccessException>();
    }

    [Test]
    public async Task Bracket_access_with_dangerous_string_is_blocked()
    {
        using var doc = JsonDocument.Parse("{\"obj\":{}}");
        await Assert.That(() => Parser.Evaluate("obj[\"__proto__\"]", doc)).Throws<AccessException>();
    }

    [Test]
    public async Task Variable_named___proto___is_blocked()
    {
        await Assert.That(() => Parser.Evaluate("__proto__")).Throws<AccessException>();
    }

    // ---------- Array index validation ----------

    [Test]
    public async Task Array_index_must_be_integer()
    {
        using var doc = JsonDocument.Parse("{\"xs\":[1,2,3]}");
        await Assert.That(() => Parser.Evaluate("xs[1.5]", doc)).Throws<ExpressionArgumentException>();
    }

    [Test]
    public async Task Array_index_out_of_range_returns_undefined()
    {
        using var doc = JsonDocument.Parse("{\"xs\":[1,2,3]}");
        var result = Parser.Evaluate("xs[99]", doc);
        await Assert.That(result).IsSameReferenceAs(Value.Undefined.Instance);
    }

    // ---------- DangerousProperties set ----------

    [Test]
    public async Task DangerousProperties_contains_expected_names()
    {
        await Assert.That(ExpressionValidator.DangerousProperties).Contains("__proto__");
        await Assert.That(ExpressionValidator.DangerousProperties).Contains("prototype");
        await Assert.That(ExpressionValidator.DangerousProperties).Contains("constructor");
    }

    // ---------- Validator API ----------

    [Test]
    public async Task ValidateVariableName_throws_on_dangerous_property()
    {
        await Assert.That(() => ExpressionValidator.ValidateVariableName("__proto__")).Throws<AccessException>();
        await Assert.That(() => ExpressionValidator.ValidateVariableName("safeName")).ThrowsNothing();
    }

    [Test]
    public async Task ValidateMemberAccess_throws_on_dangerous_property()
    {
        await Assert.That(() => ExpressionValidator.ValidateMemberAccess("constructor")).Throws<AccessException>();
        await Assert.That(() => ExpressionValidator.ValidateMemberAccess("foo")).ThrowsNothing();
    }

    [Test]
    public async Task ValidateArrayAccess_rejects_non_integer()
    {
        await Assert.That(() => ExpressionValidator.ValidateArrayAccess(Value.Array.Empty, Value.Number.Of(1.5))).Throws<ExpressionArgumentException>();
        await Assert.That(() => ExpressionValidator.ValidateArrayAccess(Value.Array.Empty, Value.Number.Of(1))).ThrowsNothing();
    }

    [Test]
    public async Task ValidateFunctionCall_rejects_non_function()
    {
        await Assert.That(() => ExpressionValidator.ValidateFunctionCall(Value.Number.Of(1), "nope")).Throws<FunctionException>();
    }

    // ---------- Error handler ----------

    [Test]
    public async Task Default_error_handler_rethrows_parse_errors()
    {
        await Assert.That(() => Parser.Evaluate("1 +")).Throws<ParseException>();
    }

    [Test]
    public async Task ThrowingErrorHandler_returns_Rethrow_for_parse_and_eval_errors()
    {
        var handler = ThrowingErrorHandler.Instance;
        await Assert.That(handler.OnParseError(new ParseException("x"))).IsTypeOf<ErrorDisposition.Rethrow>();
        await Assert.That(handler.OnEvaluationError(new EvaluationException("x"))).IsTypeOf<ErrorDisposition.Rethrow>();
    }

    [Test]
    public async Task ErrorDisposition_Abort_and_Rethrow_are_singletons()
    {
        await Assert.That(ErrorDisposition.Abort.Instance).IsSameReferenceAs(ErrorDisposition.Abort.Instance);
        await Assert.That(ErrorDisposition.Rethrow.Instance).IsSameReferenceAs(ErrorDisposition.Rethrow.Instance);
    }
}
