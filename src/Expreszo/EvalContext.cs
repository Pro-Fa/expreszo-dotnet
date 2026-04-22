using Expreszo.Errors;

namespace Expreszo;

/// <summary>
/// Per-evaluation state threaded through the walker. Carries the current
/// <see cref="Scope"/>, the optional <see cref="CancellationToken"/>, the
/// pluggable <see cref="IErrorHandler"/>, and an optional per-call resolver.
/// </summary>
/// <remarks>
/// A fresh <see cref="EvalContext"/> is created at the start of every
/// <c>Expression.Evaluate</c> / <c>Expression.EvaluateAsync</c> call. Callers
/// don't construct these directly; they're wired up internally by the
/// evaluator. Registered functions receive one as their second argument and
/// can use <c>ctx.Scope.CreateChild()</c> for lambda bodies or propagate
/// <c>ctx.CancellationToken</c> into their own async work.
/// </remarks>
public sealed class EvalContext
{
    public EvalContext(
        Scope scope,
        IErrorHandler errorHandler,
        VariableResolver? resolver = null,
        CancellationToken cancellationToken = default)
    {
        Scope = scope;
        ErrorHandler = errorHandler;
        Resolver = resolver;
        CancellationToken = cancellationToken;
    }

    public Scope Scope { get; }

    public IErrorHandler ErrorHandler { get; }

    public VariableResolver? Resolver { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a derived context for a nested call (lambda body, function
    /// definition call), pushing a child scope while preserving the error
    /// handler, resolver, and cancellation token.
    /// </summary>
    public EvalContext WithChildScope()
    {
        return new EvalContext(
            Scope.CreateChild(),
            ErrorHandler,
            Resolver,
            CancellationToken);
    }
}
