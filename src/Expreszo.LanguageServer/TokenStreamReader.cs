using Expreszo.Parsing;

namespace Expreszo.LanguageServer;

/// <summary>
/// Walks a source string with the library's <see cref="Tokenizer"/> until it
/// throws or reaches EOF. Used by hover (fallback path when the AST is
/// unavailable) and semantic highlighting.
/// </summary>
internal static class TokenStreamReader
{
    /// <summary>
    /// Enumerates tokens up to the first lexical error. Swallows
    /// <see cref="Expreszo.Errors.ParseException"/> so partial buffers still
    /// produce usable highlighting and hover data.
    /// </summary>
    public static IEnumerable<Token> Tokens(string text)
    {
        var tokenizer = new Tokenizer(ParserConfig.Default, text ?? string.Empty);

        while (true)
        {
            Token token;
            try
            {
                token = tokenizer.Next();
            }
            catch (Expreszo.Errors.ParseException)
            {
                yield break;
            }

            if (token.Kind == TokenKind.Eof)
            {
                yield break;
            }

            yield return token;
        }
    }
}
