namespace Expreszo.Parsing;

/// <summary>Discriminates token categories produced by the lexer.</summary>
public enum TokenKind
{
    /// <summary>End of expression.</summary>
    Eof,
    /// <summary>Operator (<c>+</c>, <c>-</c>, <c>==</c>, <c>and</c>, <c>=&gt;</c>, ...).</summary>
    Op,
    /// <summary>Numeric literal. <see cref="Token.Number"/> holds the parsed value.</summary>
    Number,
    /// <summary>String literal (unescaped). <see cref="Token.Text"/> holds the content.</summary>
    String,
    /// <summary>Built-in literal (<c>true</c>, <c>false</c>, <c>null</c>). <see cref="Token.Const"/> holds the value.</summary>
    Const,
    /// <summary>A <c>(</c> or <c>)</c>.</summary>
    Paren,
    /// <summary>A <c>[</c> or <c>]</c>.</summary>
    Bracket,
    /// <summary>A <c>,</c>.</summary>
    Comma,
    /// <summary>Identifier (variable or function name).</summary>
    Name,
    /// <summary>A <c>;</c>.</summary>
    Semicolon,
    /// <summary>Reserved keyword (<c>case</c>, <c>when</c>, <c>then</c>, <c>else</c>, <c>end</c>).</summary>
    Keyword,
    /// <summary>A <c>{</c> or <c>}</c>.</summary>
    Brace,
}
