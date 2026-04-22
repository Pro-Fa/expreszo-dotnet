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
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"obj\":{}}");

        // Act
        Action act = () => Parser.Evaluate("obj.__proto__", doc);

        // Assert
        await Assert.That(act).Throws<AccessException>();
    }

    [Test]
    public async Task Member_access_to_constructor_is_blocked()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"obj\":{}}");

        // Act
        Action act = () => Parser.Evaluate("obj.constructor", doc);

        // Assert
        await Assert.That(act).Throws<AccessException>();
    }

    [Test]
    public async Task Member_access_to_prototype_is_blocked()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"obj\":{}}");

        // Act
        Action act = () => Parser.Evaluate("obj.prototype", doc);

        // Assert
        await Assert.That(act).Throws<AccessException>();
    }

    [Test]
    public async Task Bracket_access_with_dangerous_string_is_blocked()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"obj\":{}}");

        // Act
        Action act = () => Parser.Evaluate("obj[\"__proto__\"]", doc);

        // Assert
        await Assert.That(act).Throws<AccessException>();
    }

    [Test]
    public async Task Variable_named___proto___is_blocked()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("__proto__");

        // Assert
        await Assert.That(act).Throws<AccessException>();
    }

    // ---------- Array index validation ----------

    [Test]
    public async Task Array_index_must_be_integer()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"xs\":[1,2,3]}");

        // Act
        Action act = () => Parser.Evaluate("xs[1.5]", doc);

        // Assert
        await Assert.That(act).Throws<ExpressionArgumentException>();
    }

    [Test]
    public async Task Array_index_out_of_range_returns_undefined()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"xs\":[1,2,3]}");

        // Act
        Value result = Parser.Evaluate("xs[99]", doc);

        // Assert
        await Assert.That(result).IsSameReferenceAs(Value.Undefined.Instance);
    }

    // ---------- DangerousProperties set ----------

    [Test]
    public async Task DangerousProperties_contains_expected_names()
    {
        // Arrange

        // Act & Assert
        await Assert.That(ExpressionValidator.DangerousProperties).Contains("__proto__");
        await Assert.That(ExpressionValidator.DangerousProperties).Contains("prototype");
        await Assert.That(ExpressionValidator.DangerousProperties).Contains("constructor");
    }

    // ---------- Validator API ----------

    [Test]
    public async Task ValidateVariableName_throws_on_dangerous_property()
    {
        // Arrange

        // Act
        Action throwing = () => ExpressionValidator.ValidateVariableName("__proto__");
        Action safe = () => ExpressionValidator.ValidateVariableName("safeName");

        // Assert
        await Assert.That(throwing).Throws<AccessException>();
        await Assert.That(safe).ThrowsNothing();
    }

    [Test]
    public async Task ValidateMemberAccess_throws_on_dangerous_property()
    {
        // Arrange

        // Act
        Action throwing = () => ExpressionValidator.ValidateMemberAccess("constructor");
        Action safe = () => ExpressionValidator.ValidateMemberAccess("foo");

        // Assert
        await Assert.That(throwing).Throws<AccessException>();
        await Assert.That(safe).ThrowsNothing();
    }

    [Test]
    public async Task ValidateArrayAccess_rejects_non_integer()
    {
        // Arrange

        // Act
        Action nonInt = () =>
            ExpressionValidator.ValidateArrayAccess(Value.Array.Empty, Value.Number.Of(1.5));
        Action integer = () =>
            ExpressionValidator.ValidateArrayAccess(Value.Array.Empty, Value.Number.Of(1));

        // Assert
        await Assert.That(nonInt).Throws<ExpressionArgumentException>();
        await Assert.That(integer).ThrowsNothing();
    }

    [Test]
    public async Task ValidateFunctionCall_rejects_non_function()
    {
        // Arrange

        // Act
        Action act = () => ExpressionValidator.ValidateFunctionCall(Value.Number.Of(1), "nope");

        // Assert
        await Assert.That(act).Throws<FunctionException>();
    }

    // ---------- Error handler ----------

    [Test]
    public async Task Default_error_handler_rethrows_parse_errors()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("1 +");

        // Assert
        await Assert.That(act).Throws<ParseException>();
    }

    [Test]
    public async Task ThrowingErrorHandler_returns_Rethrow_for_parse_and_eval_errors()
    {
        // Arrange
        var handler = ThrowingErrorHandler.Instance;

        // Act
        ErrorDisposition parseDisposition = handler.OnParseError(new ParseException("x"));
        ErrorDisposition evalDisposition = handler.OnEvaluationError(new EvaluationException("x"));

        // Assert
        await Assert.That(parseDisposition).IsTypeOf<ErrorDisposition.Rethrow>();
        await Assert.That(evalDisposition).IsTypeOf<ErrorDisposition.Rethrow>();
    }

    [Test]
    public async Task ErrorDisposition_Abort_and_Rethrow_are_singletons()
    {
        // Arrange

        // Act & Assert
        await Assert
            .That(ErrorDisposition.Abort.Instance)
            .IsSameReferenceAs(ErrorDisposition.Abort.Instance);
        await Assert
            .That(ErrorDisposition.Rethrow.Instance)
            .IsSameReferenceAs(ErrorDisposition.Rethrow.Instance);
    }
}
