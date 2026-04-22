namespace Expreszo.Tests.Errors;

public class ErrorHierarchyTests
{
    [Test]
    public async Task Every_specific_exception_is_an_ExpressionException()
    {
        // Arrange

        // Act & Assert
        await Assert.That(new ParseException("x")).IsAssignableTo<ExpressionException>();
        await Assert.That(new EvaluationException("x")).IsAssignableTo<ExpressionException>();
        await Assert.That(new VariableException("foo")).IsAssignableTo<ExpressionException>();
        await Assert.That(new FunctionException("foo")).IsAssignableTo<ExpressionException>();
        await Assert.That(new AccessException("x", "bar")).IsAssignableTo<ExpressionException>();
        await Assert
            .That(new ExpressionArgumentException("x"))
            .IsAssignableTo<ExpressionException>();
        await Assert.That(new AsyncRequiredException()).IsAssignableTo<ExpressionException>();
    }

    [Test]
    public async Task VariableException_populates_context_with_variable_name()
    {
        // Arrange

        // Act
        var ex = new VariableException("myVar");

        // Assert
        await Assert.That(ex.VariableName).IsEqualTo("myVar");
        await Assert.That(ex.Context.VariableName).IsEqualTo("myVar");
        await Assert.That(ex.Message).Contains("myVar");
    }

    [Test]
    public async Task FunctionException_populates_context_with_function_name()
    {
        // Arrange

        // Act
        var ex = new FunctionException("myFunc");

        // Assert
        await Assert.That(ex.FunctionName).IsEqualTo("myFunc");
        await Assert.That(ex.Context.FunctionName).IsEqualTo("myFunc");
    }

    [Test]
    public async Task AccessException_populates_context_with_property_name()
    {
        // Arrange

        // Act
        var ex = new AccessException("denied", "__proto__");

        // Assert
        await Assert.That(ex.PropertyName).IsEqualTo("__proto__");
        await Assert.That(ex.Context.PropertyName).IsEqualTo("__proto__");
    }

    [Test]
    public async Task ExpressionArgumentException_preserves_diagnostic_fields()
    {
        // Arrange

        // Act
        var ex = new ExpressionArgumentException(
            message: "wrong type",
            functionName: "max",
            argumentIndex: 1,
            expectedType: "number",
            receivedType: "string"
        );

        // Assert
        await Assert.That(ex.FunctionName).IsEqualTo("max");
        await Assert.That(ex.ArgumentIndex).IsEqualTo(1);
        await Assert.That(ex.ExpectedType).IsEqualTo("number");
        await Assert.That(ex.ReceivedType).IsEqualTo("string");
        await Assert.That(ex.Context.ArgumentIndex).IsEqualTo(1);
    }

    [Test]
    public async Task ParseException_can_carry_position_info()
    {
        // Arrange
        var ctx = new ErrorContext
        {
            Expression = "1 +",
            Position = new ErrorPosition(1, 4),
            Span = new TextSpan(3, 3),
        };

        // Act
        var ex = new ParseException("unexpected end", ctx);

        // Assert
        await Assert.That(ex.Expression).IsEqualTo("1 +");
        await Assert.That(ex.Position!.Value.Line).IsEqualTo(1);
        await Assert.That(ex.Position!.Value.Column).IsEqualTo(4);
        await Assert.That(ex.Context.Span!.Value.Start).IsEqualTo(3);
    }

    [Test]
    public async Task Context_with_no_fields_equals_Empty_singleton()
    {
        // Arrange

        // Act
        var empty = new ErrorContext();

        // Assert
        await Assert.That(empty).IsEqualTo(ErrorContext.Empty);
    }

    [Test]
    public async Task Exception_InnerException_is_preserved()
    {
        // Arrange
        var inner = new InvalidOperationException("boom");

        // Act
        var ex = new EvaluationException("wrapped", null, inner);

        // Assert
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    // ----- ErrorDisposition -----

    [Test]
    public async Task ThrowingErrorHandler_returns_Throw_for_all_events()
    {
        // Arrange
        var h = ThrowingErrorHandler.Instance;

        // Act & Assert
        await Assert
            .That(h.OnParseError(new ParseException("x")))
            .IsTypeOf<ErrorDisposition.Rethrow>();
        await Assert
            .That(h.OnEvaluationError(new EvaluationException("x")))
            .IsTypeOf<ErrorDisposition.Rethrow>();
        // Warnings are observed silently.
        h.OnWarning("ignored", ErrorContext.Empty);
    }

    [Test]
    public async Task ErrorDisposition_Substitute_carries_replacement_value()
    {
        // Arrange

        // Act
        var d = new ErrorDisposition.Substitute(Value.Number.Of(42));

        // Assert
        await Assert.That(d.Replacement).IsEqualTo((Value)Value.Number.Of(42));
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
