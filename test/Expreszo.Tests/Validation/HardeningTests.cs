using System.Text.Json;

namespace Expreszo.Tests.Validation;

/// <summary>
/// Locks in the behaviour added in the Phase-7 security hardening pass:
/// function allow-list, resource budgets, recursion cap, tokenizer error
/// wrapping, and JSON-boundary dangerous-name filtering.
/// </summary>
public class HardeningTests
{
    private static readonly Parser Parser = new();

    // ---------- Function allow-list ----------

    [Test]
    public async Task Resolver_returned_Value_Function_with_raw_delegate_is_rejected()
    {
        // Arrange
        ExprFunc smuggled = (_, _) => ValueTask.FromResult<Value>(Value.Number.Of(42));
        VariableResolveResult Resolver(string name) =>
            name == "evil"
                ? new VariableResolveResult.Bound(new Value.Function(smuggled, "evil"))
                : VariableResolveResult.NotResolved;

        // Act
        Action act = () => Parser.Evaluate("evil(1)", values: null, resolver: Resolver);

        // Assert
        await Assert.That(act).Throws<FunctionException>();
    }

    [Test]
    public async Task Smuggled_function_with_forged_lambda_name_is_rejected()
    {
        // Arrange
        // Before the fix, IsAllowedFunction accepted anything whose name
        // starts with "__lambda_". The new check keys on an internal init-only
        // property instead of the name, so forging the name doesn't help.
        ExprFunc smuggled = (_, _) => ValueTask.FromResult<Value>(Value.Number.Of(42));
        VariableResolveResult Resolver(string name) =>
            name == "fake"
                ? new VariableResolveResult.Bound(new Value.Function(smuggled, "__lambda_forged__"))
                : VariableResolveResult.NotResolved;

        // Act
        Action act = () => Parser.Evaluate("fake(1)", values: null, resolver: Resolver);

        // Assert
        await Assert.That(act).Throws<FunctionException>();
    }

    [Test]
    public async Task Expression_lambdas_pass_the_allow_list()
    {
        // Arrange

        // Act
        Value result = Parser.Evaluate("map([1,2,3], x => x * 2)");

        // Assert
        if (result is Value.Array arr)
        {
            await Assert.That(arr.Items.Length).IsEqualTo(3);
        }
        else
        {
            Assert.Fail("expected an array");
        }
    }

    [Test]
    public async Task Registered_functions_pass_the_allow_list()
    {
        // Arrange

        // Act
        // `sum`, `range`, and arrow functions all go through EvalCall.
        Value result = Parser.Evaluate("sum(range(1, 5))");

        // Assert
        if (result is Value.Number n)
        {
            await Assert.That(n.V).IsEqualTo(1d + 2d + 3d + 4d);
        }
        else
        {
            Assert.Fail("expected a number");
        }
    }

    [Test]
    public async Task Unary_operator_resolved_as_first_class_function_passes()
    {
        // Arrange

        // Act
        // Bare `sin` resolves to a unary op Value.Function. The allow-list
        // accepts references against both Functions and UnaryOps tables.
        Action act = () => Parser.Evaluate("sin(0)");

        // Assert
        await Assert.That(act).ThrowsNothing();
    }

    // ---------- Resource budgets ----------

    [Test]
    public async Task Repeat_over_limit_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("repeat(\"A\", 2000000000)");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task PadLeft_over_limit_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("padLeft(\"x\", 2000000000)");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task Range_over_limit_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("range(0, 1000000000)");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task Range_with_infinite_arg_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("range(0, Infinity)");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task Fac_over_limit_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("fac(1000)");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task Postfix_factorial_with_infinity_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("Infinity!");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task Percentile_with_infinity_p_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("percentile([1,2,3], Infinity)");

        // Assert
        await Assert.That(act).Throws<ExpressionArgumentException>();
    }

    // ---------- Bracket-index validation parity with ValidateArrayAccess ----------

    [Test]
    public async Task Array_index_with_infinity_is_rejected()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"xs\":[1,2,3]}");

        // Act
        Action act = () => Parser.Evaluate("xs[Infinity]", doc);

        // Assert
        await Assert.That(act).Throws<ExpressionArgumentException>();
    }

    [Test]
    public async Task Array_index_with_nan_is_rejected()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"xs\":[1,2,3]}");

        // Act
        Action act = () => Parser.Evaluate("xs[NaN]", doc);

        // Assert
        await Assert.That(act).Throws<ExpressionArgumentException>();
    }

    [Test]
    public async Task String_index_with_infinity_is_rejected()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("\"abc\"[Infinity]");

        // Assert
        await Assert.That(act).Throws<ExpressionArgumentException>();
    }

    // ---------- Recursion cap ----------

    [Test]
    public async Task Runaway_recursion_throws_EvaluationException_not_StackOverflow()
    {
        // Arrange

        // Act
        Action act = () => Parser.Evaluate("f(x) = f(x); f(1)");

        // Assert
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---------- Tokenizer exception wrapping ----------

    [Test]
    public async Task Oversized_hex_literal_surfaces_as_ParseException()
    {
        // Arrange

        // Act
        // 17 hex digits overflow Int64.
        Action act = () => Parser.Evaluate("0xFFFFFFFFFFFFFFFFF");

        // Assert
        await Assert.That(act).Throws<ParseException>();
    }

    // ---------- JSON boundary dangerous-name filtering ----------

    [Test]
    public async Task Json_input_with_dangerous_key_drops_it_from_scope()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"__proto__\": 1, \"safe\": 2}");

        // Act
        Value safe = Parser.Evaluate("safe", doc);
        Action dangerous = () => Parser.Evaluate("__proto__", doc);

        // Assert
        // safe is accessible.
        if (safe is Value.Number n)
        {
            await Assert.That(n.V).IsEqualTo(2d);
        }
        else
        {
            Assert.Fail("expected a number");
        }
        // __proto__ never made it into scope, so the access block fires.
        await Assert.That(dangerous).Throws<AccessException>();
    }

    [Test]
    public async Task Scope_ToJsonString_skips_dangerous_keys()
    {
        // Arrange
        var scope = new Scope();
        scope.SetLocal("__proto__", Value.Number.Of(1));
        scope.SetLocal("safe", Value.Number.Of(2));

        // Act
        string json = scope.ToJsonString();

        // Assert
        await Assert.That(json).DoesNotContain("__proto__");
        await Assert.That(json).Contains("safe");
    }

    // ---------- Cancellation ----------

    [Test]
    public async Task Cancellation_inside_map_loop_is_observed()
    {
        // Arrange
        // Cancel the token before we launch the evaluation so the first
        // cancellation check fires immediately. Exercises both the EvalCall
        // check and the per-iteration check inside map.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        ValueTask<Value> task = Parser.EvaluateAsync(
            "map([1,2,3], x => x * 2)",
            values: null,
            resolver: null,
            cancellationToken: cts.Token
        );

        // Assert
        await Assert.That(async () => await task).Throws<OperationCanceledException>();
    }

    // ---------- Error message scrubbing ----------

    [Test]
    public async Task ArrayIndex_error_message_reports_type_not_value()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"xs\":[1,2,3], \"password\":\"hunter2\"}");

        // Act
        ExpressionArgumentException? captured = null;
        try
        {
            Parser.Evaluate("xs[password]", doc);
        }
        catch (ExpressionArgumentException ex)
        {
            captured = ex;
        }

        // Assert
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).DoesNotContain("hunter2");
        await Assert.That(captured.Message).Contains("string");
    }
}
