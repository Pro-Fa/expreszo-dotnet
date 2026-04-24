using System.Collections.Immutable;
using Expreszo.Errors;

namespace Expreszo;

/// <summary>
/// Outcome of <see cref="Parser.TryParse(string)"/>: a best-effort
/// <see cref="Expression"/> covering the parts of the source that parsed
/// successfully, plus any diagnostics collected along the way.
/// </summary>
/// <remarks>
/// Unlike <see cref="Parser.Parse(string)"/>, this entry point never throws
/// for well-formed calls — it converts parse failures into
/// <see cref="ExpressionException"/> values in <see cref="Errors"/>. When
/// every statement fails, <see cref="Expression"/> is an empty
/// <c>undefined</c>-valued expression so callers can still walk the AST
/// without null-checking.
/// </remarks>
public sealed record ParseResult(Expression Expression, ImmutableArray<ExpressionException> Errors)
{
    /// <summary>True when at least one error was recorded.</summary>
    public bool HasErrors => !Errors.IsDefaultOrEmpty && Errors.Length > 0;
}
