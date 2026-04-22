using System.Collections.Frozen;
using Expreszo.Errors;

namespace Expreszo.Validation;

/// <summary>
/// Runtime safety checks applied by the evaluator. Blocks prototype-pollution
/// style attacks (<c>__proto__</c>, <c>prototype</c>, <c>constructor</c>),
/// validates array indices, and allow-lists callable function values so only
/// registered implementations can be invoked from an expression.
/// </summary>
public static class ExpressionValidator
{
    /// <summary>
    /// Property / variable names that are never allowed, matching the TS
    /// library's block-list. Exposed so callers can reuse it when performing
    /// their own pre-validation.
    /// </summary>
    public static readonly FrozenSet<string> DangerousProperties = new[]
    {
        "__proto__",
        "prototype",
        "constructor",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Throws <see cref="AccessException"/> if the variable name is on the block-list.</summary>
    public static void ValidateVariableName(string name, string? expression = null)
    {
        if (DangerousProperties.Contains(name))
        {
            throw new AccessException(
                Messages.MemberAccessDenied(name),
                name,
                new ErrorContext { Expression = expression }
            );
        }
    }

    /// <summary>Throws <see cref="AccessException"/> if the member property name is on the block-list.</summary>
    public static void ValidateMemberAccess(string propertyName, string? expression = null)
    {
        if (DangerousProperties.Contains(propertyName))
        {
            throw new AccessException(
                Messages.MemberAccessDenied(propertyName),
                propertyName,
                new ErrorContext { Expression = expression }
            );
        }
    }

    /// <summary>Throws <see cref="ExpressionArgumentException"/> if an array index isn't a non-negative integer.</summary>
    public static void ValidateArrayAccess(Value parent, Value index)
    {
        if (parent is not Value.Array)
        {
            return;
        }

        if (index is not Value.Number n)
        {
            throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(index));
        }
        if (n.V != Math.Floor(n.V) || double.IsNaN(n.V) || double.IsInfinity(n.V))
        {
            throw new ExpressionArgumentException(Messages.ArrayIndexNotInteger(n.V));
        }
    }

    /// <summary>Throws <see cref="ExpressionArgumentException"/> when a required argument was not supplied.</summary>
    public static void ValidateRequiredParameter(Value? value, string parameterName)
    {
        if (value is null || value is Value.Undefined)
        {
            throw new ExpressionArgumentException(
                $"required parameter '{parameterName}' is missing"
            );
        }
    }

    /// <summary>
    /// Ensures the callable <paramref name="value"/> is really a function.
    /// Raises <see cref="FunctionException"/> otherwise.
    /// </summary>
    public static void ValidateFunctionCall(Value value, string functionName)
    {
        if (value is not Value.Function)
        {
            throw new FunctionException(functionName, message: Messages.NotCallable(functionName));
        }
    }

    /// <summary>
    /// Verifies that an <see cref="ExprFunc"/> value originated from the
    /// registered operator / function table. Rejects raw delegates the user
    /// might try to smuggle through a custom resolver or <see cref="Scope"/>
    /// binding - they could otherwise call arbitrary .NET code.
    /// </summary>
    public static bool IsAllowedFunction(
        Value value,
        IReadOnlyDictionary<string, ExprFunc> registered
    )
    {
        if (value is not Value.Function fn)
        {
            return true;
        }
        // Evaluator-minted lambdas carry an internal marker. The setter is
        // `internal init`, so a caller outside the assembly cannot forge it.
        if (fn.IsExpressionLambda)
        {
            return true;
        }

        foreach (KeyValuePair<string, ExprFunc> kv in registered)
        {
            if (ReferenceEquals(kv.Value, fn.Invoke))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Throws <see cref="FunctionException"/> when a function is not on the allow-list.</summary>
    public static void ValidateAllowedFunction(
        Value value,
        IReadOnlyDictionary<string, ExprFunc> registered
    )
    {
        if (!IsAllowedFunction(value, registered))
        {
            RejectNotAllowed(value);
        }
    }

    /// <summary>
    /// Allow-list check against a precomputed set of callable implementations.
    /// The evaluator uses this path because <see cref="OperatorTable"/> builds
    /// the set once at parser-construction time, making the check O(1) per
    /// function call instead of O(n) over the registered dictionaries.
    /// </summary>
    internal static void ValidateAllowedFunction(Value value, FrozenSet<ExprFunc> callable)
    {
        if (value is not Value.Function fn)
        {
            return;
        }

        if (fn.IsExpressionLambda)
        {
            return;
        }

        if (callable.Contains(fn.Invoke))
        {
            return;
        }

        RejectNotAllowed(value);
    }

    private static void RejectNotAllowed(Value value)
    {
        string name = (value as Value.Function)?.Name ?? "<anonymous>";
        throw new FunctionException(
            name,
            message: $"function '{name}' is not permitted: only registered functions may be invoked from expressions"
        );
    }
}
