namespace Expreszo.Errors;

/// <summary>
/// Customisable policy for what happens when the parser or evaluator fails.
/// Default implementation (<see cref="ThrowingErrorHandler"/>) rethrows every
/// exception; callers can override on the parser or per-call to substitute
/// values, abort, or observe.
/// </summary>
public interface IErrorHandler
{
    ErrorDisposition OnParseError(ParseException exception);
    ErrorDisposition OnEvaluationError(ExpressionException exception);
    void OnWarning(string message, ErrorContext context);
}

/// <summary>
/// Default handler: parse and evaluation failures rethrow; warnings are ignored.
/// </summary>
public sealed class ThrowingErrorHandler : IErrorHandler
{
    public static ThrowingErrorHandler Instance { get; } = new();

    public ErrorDisposition OnParseError(ParseException exception) =>
        ErrorDisposition.Rethrow.Instance;

    public ErrorDisposition OnEvaluationError(ExpressionException exception) =>
        ErrorDisposition.Rethrow.Instance;

    public void OnWarning(string message, ErrorContext context) { }
}
