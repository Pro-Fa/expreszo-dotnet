using System.Globalization;
using Expreszo.Errors;

namespace Expreszo.Parsing;

/// <summary>
/// Stateful, forward-only lexer. Internal; consumers go through
/// <see cref="TokenCursor"/>.
/// </summary>
/// <remarks>
/// Ordering quirks worth knowing about: radix integers are matched before
/// general numbers, named operators (<c>and</c> / <c>or</c> / <c>not in</c> /
/// <c>as</c>) override the general identifier rule when a name resolves to an
/// enabled operator, and the question-mark operator disambiguates between
/// <c>?</c> and <c>??</c>.
/// </remarks>
internal sealed class Tokenizer(ParserConfig config, string? expression)
{
    private static readonly HashSet<char> SingleCharOperators =
    [
        '+',
        '-',
        '*',
        '/',
        '%',
        '^',
        ':',
        '.',
    ];

    private static readonly HashSet<char> MultiplicationSymbols =
    [
        '∙', // ∙
        '•', // •
    ];

    private readonly string _expression = expression ?? string.Empty;
    private int _pos;
    private Token? _current;

    public string Expression => _expression;
    public int Position => _pos;

    /// <summary>
    /// Returns the next token, skipping whitespace and comments. Raises
    /// <see cref="ParseException"/> on unrecognised characters. Returns a
    /// fresh EOF token each time once the end of input is reached.
    /// </summary>
    public Token Next()
    {
        while (true)
        {
            if (_pos >= _expression.Length)
            {
                return NewToken(TokenKind.Eof, "EOF", _pos, _pos);
            }

            if (ConsumeWhitespace() || ConsumeComment())
            {
                continue;
            }

            if (
                IsRadixInteger()
                || IsNumber()
                || IsOperator()
                || IsString()
                || IsParen()
                || IsBrace()
                || IsBracket()
                || IsComma()
                || IsSemicolon()
                || IsNamedOp()
                || IsConst()
                || IsName()
            )
            {
                return _current!;
            }

            throw ParseError($"Unknown character \"{CharAt(_pos)}\"");
        }
    }

    // ---------------- helpers ----------------

    private static Token NewToken(
        TokenKind kind,
        string text,
        int start,
        int end,
        double number = 0d,
        Value? constValue = null
    ) => new(kind, text, start, end, number, constValue);

    private char CharAt(int index) =>
        index < 0 || index >= _expression.Length ? '\0' : _expression[index];

    private bool StartsWithAt(int index, string s)
    {
        if (index < 0 || index + s.Length > _expression.Length)
        {
            return false;
        }
        return _expression.AsSpan(index, s.Length).SequenceEqual(s.AsSpan());
    }

    private static bool IsLetter(char c)
    {
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
        {
            return true;
        }
        if (c < 128)
        {
            return false;
        }
        // Non-ASCII: treat as a letter if it's case-variable.
        return char.ToUpperInvariant(c) != char.ToLowerInvariant(c);
    }

    // ---------------- whitespace / comments ----------------

    private bool ConsumeWhitespace()
    {
        bool r = false;
        while (_pos < _expression.Length)
        {
            char c = _expression[_pos];
            if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
            {
                break;
            }
            _pos++;
            r = true;
        }
        return r;
    }

    private bool ConsumeComment()
    {
        char c = CharAt(_pos);
        if (c == '/' && CharAt(_pos + 1) == '*')
        {
            int end = _expression.IndexOf("*/", _pos, StringComparison.Ordinal);
            _pos = end < 0 ? _expression.Length : end + 2;
            return true;
        }
        if (c == '/' && CharAt(_pos + 1) == '/')
        {
            int newline = _expression.IndexOf('\n', _pos + 2);
            _pos = newline < 0 ? _expression.Length : newline + 1;
            return true;
        }
        return false;
    }

    // ---------------- simple punctuation ----------------

    private bool IsParen()
    {
        char c = CharAt(_pos);
        if (c == '(' || c == ')')
        {
            _current = NewToken(TokenKind.Paren, c.ToString(), _pos, _pos + 1);
            _pos++;
            return true;
        }
        return false;
    }

    private bool IsBrace()
    {
        char c = CharAt(_pos);
        if (c == '{' || c == '}')
        {
            _current = NewToken(TokenKind.Brace, c.ToString(), _pos, _pos + 1);
            _pos++;
            return true;
        }
        return false;
    }

    private bool IsBracket()
    {
        char c = CharAt(_pos);
        if ((c == '[' || c == ']') && config.IsOperatorEnabled("["))
        {
            _current = NewToken(TokenKind.Bracket, c.ToString(), _pos, _pos + 1);
            _pos++;
            return true;
        }
        return false;
    }

    private bool IsComma()
    {
        if (CharAt(_pos) == ',')
        {
            _current = NewToken(TokenKind.Comma, ",", _pos, _pos + 1);
            _pos++;
            return true;
        }
        return false;
    }

    private bool IsSemicolon()
    {
        if (CharAt(_pos) == ';')
        {
            _current = NewToken(TokenKind.Semicolon, ";", _pos, _pos + 1);
            _pos++;
            return true;
        }
        return false;
    }

    // ---------------- numbers ----------------

    private bool IsRadixInteger()
    {
        // Matches 0x.../0b... before the general number rule so it wins.
        int pos = _pos;
        if (pos >= _expression.Length - 2 || _expression[pos] != '0')
        {
            return false;
        }
        pos++;
        int radix;
        Func<char, bool> validDigit;
        if (_expression[pos] == 'x')
        {
            radix = 16;
            validDigit = c =>
                (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            pos++;
        }
        else if (_expression[pos] == 'b')
        {
            radix = 2;
            validDigit = c => c == '0' || c == '1';
            pos++;
        }
        else
        {
            return false;
        }

        int startDigits = pos;
        bool valid = false;
        while (pos < _expression.Length && validDigit(_expression[pos]))
        {
            pos++;
            valid = true;
        }

        if (!valid)
        {
            return false;
        }

        string digits = _expression[startDigits..pos];
        double value;
        try
        {
            value = (double)Convert.ToInt64(digits, radix);
        }
        catch (OverflowException ex)
        {
            throw new ParseException(
                $"radix integer literal is too large to fit in Int64: {_expression[_pos..pos]}",
                new ErrorContext { Expression = _expression, Position = GetCoordinatesAt(_pos) },
                innerException: ex
            );
        }
        catch (FormatException ex)
        {
            throw new ParseException(
                $"malformed radix integer literal: {_expression[_pos..pos]}",
                new ErrorContext { Expression = _expression, Position = GetCoordinatesAt(_pos) },
                innerException: ex
            );
        }
        _current = NewToken(TokenKind.Number, _expression[_pos..pos], _pos, pos, number: value);
        _pos = pos;
        return true;
    }

    private bool IsNumber()
    {
        int pos = _pos;
        int startPos = pos;
        int resetPos = pos;
        bool foundDot = false;
        bool foundDigits = false;
        bool valid = false;
        char c = '\0';

        while (pos < _expression.Length)
        {
            c = _expression[pos];
            if ((c >= '0' && c <= '9') || (!foundDot && c == '.'))
            {
                if (c == '.')
                {
                    foundDot = true;
                }
                else
                {
                    foundDigits = true;
                }
                pos++;
                valid = foundDigits;
            }
            else
            {
                break;
            }
        }

        if (valid)
        {
            resetPos = pos;
        }

        // Peek the trailing char. (In TS `c!` is used to reference the last
        // loop char; here we carry it forward explicitly.)
        char trailing = pos < _expression.Length ? _expression[pos] : c;
        if (trailing is 'e' or 'E')
        {
            pos++;
            bool acceptSign = true;
            bool validExponent = false;
            while (pos < _expression.Length)
            {
                char ec = _expression[pos];
                if (acceptSign && (ec == '+' || ec == '-'))
                {
                    acceptSign = false;
                }
                else if (ec >= '0' && ec <= '9')
                {
                    validExponent = true;
                    acceptSign = false;
                }
                else
                {
                    break;
                }
                pos++;
            }
            if (!validExponent)
            {
                pos = resetPos;
            }
        }

        if (valid)
        {
            string slice = _expression[startPos..pos];
            if (
                !double.TryParse(
                    slice,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number
                )
            )
            {
                throw new ParseException(
                    $"malformed numeric literal: {slice}",
                    new ErrorContext
                    {
                        Expression = _expression,
                        Position = GetCoordinatesAt(startPos),
                    }
                );
            }
            _current = NewToken(TokenKind.Number, slice, startPos, pos, number: number);
            _pos = pos;
        }
        else
        {
            _pos = resetPos;
        }
        return valid;
    }

    // ---------------- strings ----------------

    private bool IsString()
    {
        int startPos = _pos;
        char quote = CharAt(startPos);
        if (quote != '\'' && quote != '"')
        {
            return false;
        }

        // Scan for the matching closing quote, counting preceding backslashes
        // to distinguish escaped quotes.
        int index = _expression.IndexOf(quote, startPos + 1);
        while (index >= 0 && _pos < _expression.Length)
        {
            int advanceTo = index + 1;
            int backslashCount = 0;
            int checkPos = index - 1;
            while (checkPos >= startPos + 1 && _expression[checkPos] == '\\')
            {
                backslashCount++;
                checkPos--;
            }
            if (backslashCount % 2 == 0)
            {
                string raw = _expression[(startPos + 1)..index];
                string unescaped = Unescape(raw);
                _current = NewToken(TokenKind.String, unescaped, startPos, advanceTo);
                _pos = advanceTo;
                return true;
            }
            index = _expression.IndexOf(quote, index + 1);
        }
        return false;
    }

    private string Unescape(string v)
    {
        int index = v.IndexOf('\\');
        if (index < 0)
        {
            return v;
        }

        var sb = new System.Text.StringBuilder(v.Length);
        sb.Append(v, 0, index);
        int currentIndex = index;

        while (currentIndex >= 0)
        {
            currentIndex++;
            if (currentIndex >= v.Length)
            {
                throw ParseError("Illegal escape sequence at end of string");
            }
            char c = v[currentIndex];
            (string ch, int skip) = ProcessEscapeChar(c, v, currentIndex);
            sb.Append(ch);
            currentIndex += skip;

            currentIndex++;
            int nextBackslash = v.IndexOf('\\', currentIndex);
            int end = nextBackslash < 0 ? v.Length : nextBackslash;
            sb.Append(v, currentIndex, end - currentIndex);
            currentIndex = nextBackslash;
        }

        return sb.ToString();
    }

    private (string Char, int Skip) ProcessEscapeChar(char c, string v, int currentIndex)
    {
        switch (c)
        {
            case '\'':
                return ("'", 0);
            case '"':
                return ("\"", 0);
            case '\\':
                return ("\\", 0);
            case '/':
                return ("/", 0);
            case 'b':
                return ("\b", 0);
            case 'f':
                return ("\f", 0);
            case 'n':
                return ("\n", 0);
            case 'r':
                return ("\r", 0);
            case 't':
                return ("\t", 0);
            case 'u':
            {
                if (currentIndex + 4 >= v.Length)
                {
                    throw ParseError("Illegal unicode escape sequence");
                }
                string hex = v.Substring(currentIndex + 1, 4);
                if (
                    !ushort.TryParse(
                        hex,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out ushort code
                    )
                )
                {
                    throw ParseError($"Illegal escape sequence: \\u{hex}");
                }
                return (((char)code).ToString(), 4);
            }
            default:
                throw ParseError($"Illegal escape sequence: \"\\{c}\"");
        }
    }

    // ---------------- identifiers / constants / named operators ----------------

    private bool IsConst()
    {
        int startPos = _pos;
        int i = startPos;
        for (; i < _expression.Length; i++)
        {
            char c = _expression[i];
            if (!IsLetter(c))
            {
                if (i == _pos || (c != '_' && c != '.' && (c < '0' || c > '9')))
                {
                    break;
                }
            }
        }
        if (i <= startPos)
        {
            return false;
        }
        string str = _expression[startPos..i];
        if (config.NumericConstants.TryGetValue(str, out double number))
        {
            _current = NewToken(
                TokenKind.Number,
                str,
                startPos,
                startPos + str.Length,
                number: number
            );
            _pos += str.Length;
            return true;
        }
        if (config.BuiltinLiterals.TryGetValue(str, out Value? literal))
        {
            _current = NewToken(
                TokenKind.Const,
                str,
                startPos,
                startPos + str.Length,
                constValue: literal
            );
            _pos += str.Length;
            return true;
        }
        return false;
    }

    private bool IsNamedOp()
    {
        int startPos = _pos;
        int i = startPos;
        for (; i < _expression.Length; i++)
        {
            char c = _expression[i];
            if (!IsLetter(c))
            {
                if (i == _pos || (c != '_' && (c < '0' || c > '9')))
                {
                    break;
                }
            }
        }
        if (i <= startPos)
        {
            return false;
        }
        string str = _expression[startPos..i];
        if (str == "not")
        {
            // Look ahead for the compound 'not in' named op.
            if (StartsWithAt(startPos, "not in"))
            {
                str = "not in";
            }
        }
        if (config.IsOperatorEnabled(str) && config.IsNamedOperator(str))
        {
            _current = NewToken(TokenKind.Op, str, startPos, startPos + str.Length);
            _pos += str.Length;
            return true;
        }
        return false;
    }

    private bool IsName()
    {
        int startPos = _pos;
        int i = startPos;
        bool hasLetter = false;
        bool leadingDollar = false;
        for (; i < _expression.Length; i++)
        {
            char c = _expression[i];
            if (!IsLetter(c))
            {
                if (i == _pos && (c == '$' || c == '_'))
                {
                    if (c == '_')
                    {
                        hasLetter = true;
                    }
                    else
                    {
                        leadingDollar = true;
                    }
                    continue;
                }
                if (i == startPos + 1 && leadingDollar && c == '$')
                {
                    // allow $$name tokens
                    continue;
                }
                if (i == _pos || !hasLetter || (c != '_' && (c < '0' || c > '9')))
                {
                    break;
                }
            }
            else
            {
                hasLetter = true;
            }
        }
        if (!hasLetter)
        {
            return false;
        }
        string str = _expression[startPos..i];
        TokenKind kind = config.Keywords.Contains(str) ? TokenKind.Keyword : TokenKind.Name;
        _current = NewToken(kind, str, startPos, startPos + str.Length);
        _pos += str.Length;
        return true;
    }

    // ---------------- operators ----------------

    private bool IsOperator()
    {
        int startPos = _pos;
        char c = CharAt(_pos);

        // Spread '...' before single-char '.'
        if (c == '.' && CharAt(_pos + 1) == '.' && CharAt(_pos + 2) == '.')
        {
            _current = NewToken(TokenKind.Op, "...", startPos, startPos + 3);
            _pos += 2;
            // falls through to the shared increment below
        }
        else if (SingleCharOperators.Contains(c))
        {
            _current = NewToken(TokenKind.Op, c.ToString(), startPos, startPos + 1);
        }
        else if (MultiplicationSymbols.Contains(c))
        {
            _current = NewToken(TokenKind.Op, "*", startPos, startPos + 1);
        }
        else if (c == '?')
        {
            if (CharAt(_pos + 1) == '?')
            {
                if (!config.IsOperatorEnabled("??"))
                {
                    return false;
                }
                _current = NewToken(TokenKind.Op, "??", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                _current = NewToken(TokenKind.Op, "?", startPos, startPos + 1);
            }
        }
        else if (c == '>')
        {
            if (CharAt(_pos + 1) == '=')
            {
                _current = NewToken(TokenKind.Op, ">=", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                _current = NewToken(TokenKind.Op, ">", startPos, startPos + 1);
            }
        }
        else if (c == '<')
        {
            if (CharAt(_pos + 1) == '=')
            {
                _current = NewToken(TokenKind.Op, "<=", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                _current = NewToken(TokenKind.Op, "<", startPos, startPos + 1);
            }
        }
        else if (c == '=')
        {
            if (CharAt(_pos + 1) == '>')
            {
                if (!config.IsOperatorEnabled("=>"))
                {
                    return false;
                }
                _current = NewToken(TokenKind.Op, "=>", startPos, startPos + 2);
                _pos++;
            }
            else if (CharAt(_pos + 1) == '=')
            {
                _current = NewToken(TokenKind.Op, "==", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                _current = NewToken(TokenKind.Op, "=", startPos, startPos + 1);
            }
        }
        else if (c == '!')
        {
            if (CharAt(_pos + 1) == '=')
            {
                _current = NewToken(TokenKind.Op, "!=", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                _current = NewToken(TokenKind.Op, "!", startPos, startPos + 1);
            }
        }
        else if (c == '|')
        {
            if (CharAt(_pos + 1) == '|')
            {
                _current = NewToken(TokenKind.Op, "||", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                _current = NewToken(TokenKind.Op, "|", startPos, startPos + 1);
            }
        }
        else if (c == '&')
        {
            if (CharAt(_pos + 1) == '&')
            {
                _current = NewToken(TokenKind.Op, "&&", startPos, startPos + 2);
                _pos++;
            }
            else
            {
                return false;
            }
        }
        else if (c == 'a' && StartsWithAt(_pos, "as "))
        {
            if (!config.IsOperatorEnabled("as"))
            {
                return false;
            }
            _current = NewToken(TokenKind.Op, "as", startPos, startPos + 2);
            // 'as' is two characters; bump now, the shared ++ below adds the
            // second one.
            _pos++;
        }
        else
        {
            return false;
        }

        _pos++;

        // Final enablement gate. '...' and named ops we already gated; everything else checks now.
        if (_current!.Text == "..." || config.IsOperatorEnabled(_current.Text))
        {
            return true;
        }

        _pos = startPos;
        _current = null;
        return false;
    }

    // ---------------- error surface ----------------

    public ErrorPosition GetCoordinates()
    {
        int line = 0;
        int column = 0;
        int newline = -1;
        do
        {
            line++;
            column = _pos - newline;
            newline = _expression.IndexOf('\n', newline + 1);
        } while (newline >= 0 && newline < _pos);
        return new ErrorPosition(line, column);
    }

    public ErrorPosition GetCoordinatesAt(int pos)
    {
        int line = 0;
        int column = 0;
        int newline = -1;
        do
        {
            line++;
            column = pos - newline;
            newline = _expression.IndexOf('\n', newline + 1);
        } while (newline >= 0 && newline < pos);
        return new ErrorPosition(line, column);
    }

    internal ParseException ParseError(string message)
    {
        return new ParseException(
            message,
            new ErrorContext { Expression = _expression, Position = GetCoordinates() }
        );
    }
}
