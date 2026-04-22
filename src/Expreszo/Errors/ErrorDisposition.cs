namespace Expreszo.Errors;

/// <summary>
/// Pluggable decision an <see cref="IErrorHandler"/> returns when it sees a
/// parse or evaluation error. The walker interprets the disposition:
/// <list type="bullet">
///   <item><see cref="Rethrow"/> - rethrow the original exception (default).</item>
///   <item><see cref="Substitute"/> - return <c>Replacement</c> in place of the
///   failed subexpression and keep evaluating.</item>
///   <item><see cref="Abort"/> - stop evaluation cleanly and return
///   <see cref="Value.Undefined"/> as the overall result.</item>
/// </list>
/// </summary>
public abstract record ErrorDisposition
{
    public sealed record Rethrow : ErrorDisposition
    {
        public static Rethrow Instance { get; } = new();
    }

    public sealed record Substitute(Value Replacement) : ErrorDisposition;

    public sealed record Abort : ErrorDisposition
    {
        public static Abort Instance { get; } = new();
    }
}
