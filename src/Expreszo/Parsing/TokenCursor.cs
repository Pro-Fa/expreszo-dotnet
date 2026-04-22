using System.Collections.Immutable;
using Expreszo.Errors;

namespace Expreszo.Parsing;

/// <summary>
/// Immutable view over a pre-tokenised expression. Backtracking is a single
/// assignment (<c>cursor = saved</c>) instead of a mutable save/restore dance.
/// Port of <c>src/parsing/token-cursor.ts</c>.
/// </summary>
/// <remarks>
/// The cursor eagerly drains a <see cref="Tokenizer"/> at construction time so
/// the downstream parser only ever reads an indexed array. Advancing produces
/// a new <see cref="TokenCursor"/> that shares the token array and only
/// differs in index — allocation cost per advance is two machine words.
/// </remarks>
internal sealed class TokenCursor
{
    public ImmutableArray<Token> Tokens { get; }
    public int Index { get; }
    public string Expression { get; }

    private TokenCursor(ImmutableArray<Token> tokens, int index, string expression)
    {
        Tokens = tokens;
        Index = index;
        Expression = expression;
    }

    /// <summary>
    /// Drains the tokenizer into a flat array and returns a cursor positioned
    /// at the first token. Tokenisation errors surface at this point rather
    /// than on demand during parsing — that's a benign difference vs the TS
    /// library since malformed tokens could never be parsed anyway.
    /// </summary>
    public static TokenCursor From(TokenizerConfig config, string expression)
    {
        var tokenizer = new Tokenizer(config, expression);
        var builder = ImmutableArray.CreateBuilder<Token>();
        while (true)
        {
            var token = tokenizer.Next();
            builder.Add(token);
            if (token.Kind == TokenKind.Eof)
            {
                break;
            }
        }
        return new TokenCursor(builder.ToImmutable(), 0, expression);
    }

    /// <summary>Token at the current position; does not advance.</summary>
    public Token Peek() => Tokens[Index];

    /// <summary>
    /// Token at <paramref name="offset"/> positions past the current cursor.
    /// Reads past the end clamp to the terminating EOF token — the parser can
    /// look arbitrarily far ahead without bounds-checking.
    /// </summary>
    public Token PeekAt(int offset)
    {
        var i = Index + offset;
        if (i < 0)
        {
            return Tokens[0];
        }
        if (i >= Tokens.Length)
        {
            return Tokens[^1];
        }
        return Tokens[i];
    }

    public bool AtEnd => Peek().Kind == TokenKind.Eof;

    /// <summary>Exclusive end offset of the current token.</summary>
    public int PeekEnd() => Tokens[Index].End;

    /// <summary>Exclusive end offset of the most recently consumed token.</summary>
    public int PreviousEnd() => Index == 0 ? Tokens[0].End : Tokens[Index - 1].End;

    /// <summary>
    /// Returns a cursor one token ahead. If already parked on EOF, returns
    /// this same cursor — EOF is the absorbing state.
    /// </summary>
    public TokenCursor Advance()
    {
        if (AtEnd)
        {
            return this;
        }
        return new TokenCursor(Tokens, Index + 1, Expression);
    }

    /// <summary>
    /// Non-advancing check for the current token's kind (and optional text).
    /// </summary>
    public bool Check(TokenKind kind, string? text = null)
    {
        var token = Peek();
        if (token.Kind != kind)
        {
            return false;
        }
        return text is null || token.Text == text;
    }

    /// <summary>
    /// If the current token matches, yields the token plus a cursor advanced
    /// past it. Otherwise returns <c>null</c> so the caller can try a
    /// different rule.
    /// </summary>
    public (Token Token, TokenCursor Next)? Match(TokenKind kind, string? text = null)
    {
        if (!Check(kind, text))
        {
            return null;
        }
        return (Peek(), Advance());
    }

    /// <summary>
    /// 1-based line/column of the current token's start position, matching
    /// <see cref="Tokenizer.GetCoordinatesAt"/>. Used when surfacing parse errors.
    /// </summary>
    public ErrorPosition GetCoordinates()
    {
        var pos = Peek().Index;
        var line = 0;
        var column = 0;
        var newline = -1;
        do
        {
            line++;
            column = pos - newline;
            newline = Expression.IndexOf('\n', newline + 1);
        }
        while (newline >= 0 && newline < pos);
        return new ErrorPosition(line, column);
    }
}
