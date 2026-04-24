using System.Collections.Immutable;
using Expreszo.Errors;

namespace Expreszo.Parsing;

/// <summary>
/// Splits source text into top-level statement segments separated by
/// semicolons, ignoring semicolons nested inside parens / brackets / braces.
/// Segments record their absolute source span, so any parse of one segment
/// can be mapped back into the full buffer.
/// </summary>
internal static class StatementSplitter
{
    /// <summary>
    /// Tokenises <paramref name="source"/> until EOF or the first lexical
    /// error and returns its top-level statement segments. The splitter is
    /// deliberately tolerant: unbalanced brackets collapse into whatever
    /// segments we can salvage, since the caller (error-recovering parser)
    /// will report the syntax issue separately.
    /// </summary>
    public static ImmutableArray<StatementSegment> Split(ParserConfig config, string source)
    {
        ArgumentNullException.ThrowIfNull(config);
        source ??= string.Empty;

        var segments = ImmutableArray.CreateBuilder<StatementSegment>();

        var tokenizer = new Tokenizer(config, source);
        int depth = 0;
        int segmentStart = 0;
        bool sawNonTrivia = false;

        while (true)
        {
            Token token;
            try
            {
                token = tokenizer.Next();
            }
            catch (ParseException)
            {
                // Tokenizer choked mid-way. Emit whatever we have so far as
                // the final segment covering to end-of-input; the caller's
                // per-segment parse will surface the underlying error with a
                // concrete span.
                break;
            }

            if (token.Kind == TokenKind.Eof)
            {
                break;
            }

            switch (token.Kind)
            {
                case TokenKind.Paren when token.Text == "(":
                case TokenKind.Bracket when token.Text == "[":
                case TokenKind.Brace when token.Text == "{":
                    depth++;
                    sawNonTrivia = true;
                    break;

                case TokenKind.Paren when token.Text == ")":
                case TokenKind.Bracket when token.Text == "]":
                case TokenKind.Brace when token.Text == "}":
                    if (depth > 0)
                    {
                        depth--;
                    }
                    sawNonTrivia = true;
                    break;

                case TokenKind.Semicolon when depth == 0:
                    if (sawNonTrivia)
                    {
                        segments.Add(new StatementSegment(segmentStart, token.Index));
                    }

                    segmentStart = token.End;
                    sawNonTrivia = false;
                    break;

                default:
                    sawNonTrivia = true;
                    break;
            }
        }

        if (sawNonTrivia)
        {
            segments.Add(new StatementSegment(segmentStart, source.Length));
        }

        return segments.ToImmutable();
    }
}

/// <summary>
/// Half-open <c>[Start, End)</c> absolute-offset range covering one
/// statement in a source buffer.
/// </summary>
internal readonly record struct StatementSegment(int Start, int End)
{
    public int Length => End - Start;

    public TextSpan AsSpan() => new(Start, End);
}
