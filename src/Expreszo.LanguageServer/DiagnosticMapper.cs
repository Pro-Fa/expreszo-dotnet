using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expreszo.LanguageServer;

/// <summary>
/// Converts <see cref="ExpressionException"/>s into LSP diagnostics, using
/// the span and position fields already carried on <see cref="ErrorContext"/>.
/// </summary>
internal static class DiagnosticMapper
{
    /// <summary>
    /// Maps <paramref name="error"/> to a single-element diagnostic list.
    /// The strict parser only ever surfaces one error at a time — multi-
    /// diagnostic publishing lands alongside error recovery in Tier 2.
    /// </summary>
    public static IEnumerable<Diagnostic> Map(ExpressionException error, LineIndex lineIndex)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(lineIndex);

        return [MapOne(error, lineIndex)];
    }

    private static Diagnostic MapOne(ExpressionException error, LineIndex lineIndex)
    {
        Range range = ResolveRange(error.Context, lineIndex);
        DiagnosticSeverity severity = error switch
        {
            ParseException => DiagnosticSeverity.Error,
            AccessException => DiagnosticSeverity.Error,
            VariableException => DiagnosticSeverity.Warning,
            FunctionException => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error,
        };

        return new Diagnostic
        {
            Range = range,
            Severity = severity,
            Source = "expreszo",
            Message = error.Message,
            Code = new DiagnosticCode(error.GetType().Name),
        };
    }

    private static Range ResolveRange(ErrorContext context, LineIndex lineIndex)
    {
        if (context.Span is { } span && span.Length > 0)
        {
            (int startLine, int startChar) = lineIndex.OffsetToPosition(span.Start);
            (int endLine, int endChar) = lineIndex.OffsetToPosition(span.End);
            return new Range(startLine, startChar, endLine, endChar);
        }

        if (context.Position is { } pos)
        {
            // ErrorContext.Position is 1-based; LSP is 0-based.
            int line = Math.Max(0, pos.Line - 1);
            int character = Math.Max(0, pos.Column - 1);
            return new Range(line, character, line, character + 1);
        }

        return new Range(0, 0, 0, 1);
    }
}
