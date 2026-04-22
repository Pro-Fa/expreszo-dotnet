namespace Expreszo.Errors;

/// <summary>
/// Base class for every exception the library raises from parse or evaluate.
/// Carries an <see cref="ErrorContext"/> with line/column/span and optional
/// variable, function, and property names. Matches the TypeScript library's
/// <c>ExpressionError</c> hierarchy.
/// </summary>
public abstract class ExpressionException : Exception
{
    protected ExpressionException(string message, ErrorContext? context = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Context = context ?? ErrorContext.Empty;
    }

    public ErrorContext Context { get; }

    /// <summary>Convenience accessor for the source expression, if available.</summary>
    public string? Expression => Context.Expression;

    /// <summary>Convenience accessor for the 1-based position, if available.</summary>
    public ErrorPosition? Position => Context.Position;
}

/// <summary>Syntax errors raised during tokenisation or parsing.</summary>
public sealed class ParseException : ExpressionException
{
    public ParseException(string message, ErrorContext? context = null, Exception? innerException = null)
        : base(message, context, innerException) { }
}

/// <summary>Generic evaluation failure — division by zero, cast errors, etc.</summary>
public sealed class EvaluationException : ExpressionException
{
    public EvaluationException(string message, ErrorContext? context = null, Exception? innerException = null)
        : base(message, context, innerException) { }
}

/// <summary>Raised when an expression references a variable the evaluator can't resolve.</summary>
public sealed class VariableException : ExpressionException
{
    public VariableException(string variableName, ErrorContext? context = null, string? message = null)
        : base(
            message ?? Messages.UndefinedVariable(variableName),
            (context ?? ErrorContext.Empty) with { VariableName = variableName })
    {
        VariableName = variableName;
    }

    public string VariableName { get; }
}

/// <summary>Raised when an expression tries to call something that isn't a registered function.</summary>
public sealed class FunctionException : ExpressionException
{
    public FunctionException(string functionName, ErrorContext? context = null, string? message = null)
        : base(
            message ?? Messages.UndefinedFunction(functionName),
            (context ?? ErrorContext.Empty) with { FunctionName = functionName })
    {
        FunctionName = functionName;
    }

    public string FunctionName { get; }
}

/// <summary>Raised for forbidden member access (e.g. <c>__proto__</c>) or disabled member access.</summary>
public sealed class AccessException : ExpressionException
{
    public AccessException(string message, string? propertyName = null, ErrorContext? context = null)
        : base(
            message,
            (context ?? ErrorContext.Empty) with { PropertyName = propertyName })
    {
        PropertyName = propertyName;
    }

    public string? PropertyName { get; }
}

/// <summary>
/// Raised when a function is called with the wrong number or type of arguments.
/// Named with the <c>Expression</c> prefix to avoid clashing with
/// <see cref="System.ArgumentException"/>.
/// </summary>
public sealed class ExpressionArgumentException : ExpressionException
{
    public ExpressionArgumentException(
        string message,
        string? functionName = null,
        int? argumentIndex = null,
        string? expectedType = null,
        string? receivedType = null,
        ErrorContext? context = null)
        : base(
            message,
            (context ?? ErrorContext.Empty) with
            {
                FunctionName = functionName,
                ArgumentIndex = argumentIndex,
                ExpectedType = expectedType,
                ReceivedType = receivedType,
            })
    {
        FunctionName = functionName;
        ArgumentIndex = argumentIndex;
        ExpectedType = expectedType;
        ReceivedType = receivedType;
    }

    public string? FunctionName { get; }
    public int? ArgumentIndex { get; }
    public string? ExpectedType { get; }
    public string? ReceivedType { get; }
}

/// <summary>
/// Raised by synchronous <c>Evaluate</c> when the expression requires async
/// evaluation (i.e. a registered function returned a non-completed
/// <see cref="System.Threading.Tasks.ValueTask{TResult}"/>). Callers should use
/// <c>EvaluateAsync</c> instead.
/// </summary>
public sealed class AsyncRequiredException : ExpressionException
{
    public AsyncRequiredException(ErrorContext? context = null)
        : base(Messages.AsyncRequired(), context) { }
}
