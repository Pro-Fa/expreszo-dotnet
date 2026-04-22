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
        int startPos = _pos;
        (int mantissaEnd, bool hasDigits) = ScanMantissa(startPos);
        if (!hasDigits)
        {
            // Leave the cursor where it was so the caller can try other rules.
            return false;
        }

        int end = CharAt(mantissaEnd) is 'e' or 'E'
            ? ScanExponent(mantissaEnd, fallback: mantissaEnd)
            : mantissaEnd;

        string slice = _expression[startPos..end];
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
        _current = NewToken(TokenKind.Number, slice, startPos, end, number: number);
        _pos = end;
        return true;
    }

    /// <summary>
    /// Scans digits and at most one '.'; returns the end position and whether
    /// at least one digit was consumed. A lone leading '.' is consumed here
    /// but still reports HasDigits=false so the caller can reject the literal.
    /// </summary>
    private (int End, bool HasDigits) ScanMantissa(int startPos)
    {
        int pos = startPos;
        bool foundDot = false;
        bool foundDigits = false;
        while (pos < _expression.Length)
        {
            char c = _expression[pos];
            bool isDigit = c >= '0' && c <= '9';
            bool isFirstDot = !foundDot && c == '.';
            if (!isDigit && !isFirstDot)
            {
                break;
            }
            foundDot |= isFirstDot;
            foundDigits |= isDigit;
            pos++;
        }
        return (pos, foundDigits);
    }

    /// <summary>
    /// Scans an exponent clause starting at the 'e' or 'E' at <paramref name="markerPos"/>.
    /// Returns the position past the exponent when at least one digit was
    /// found, otherwise <paramref name="fallback"/> to signal "no exponent here".
    /// </summary>
    private int ScanExponent(int markerPos, int fallback)
    {
        int pos = markerPos + 1;
        bool acceptSign = true;
        bool seenDigit = false;
        while (pos < _expression.Length)
        {
            char c = _expression[pos];
            if (acceptSign && (c == '+' || c == '-'))
            {
                acceptSign = false;
            }
            else if (c >= '0' && c <= '9')
            {
                seenDigit = true;
                acceptSign = false;
            }
            else
            {
                break;
            }
            pos++;
        }
        return seenDigit ? pos : fallback;
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

    private (string Char, int Skip) ProcessEscapeChar(char c, string v, int currentIndex) =>
        c switch
        {
            '\'' => ("'", 0),
            '"' => ("\"", 0),
            '\\' => ("\\", 0),
            '/' => ("/", 0),
            'b' => ("\b", 0),
            'f' => ("\f", 0),
            'n' => ("\n", 0),
            'r' => ("\r", 0),
            't' => ("\t", 0),
            'u' => ProcessUnicodeEscape(v, currentIndex),
            _ => throw ParseError($"Illegal escape sequence: \"\\{c}\""),
        };

    private (string Char, int Skip) ProcessUnicodeEscape(string v, int currentIndex)
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

    // ---------------- identifiers / constants / named operators ----------------

    private bool IsConst()
    {
        int startPos = _pos;
        int end = ScanWord(startPos, allowDot: true);
        if (end == startPos)
        {
            return false;
        }
        string str = _expression[startPos..end];
        if (config.NumericConstants.TryGetValue(str, out double number))
        {
            _current = NewToken(TokenKind.Number, str, startPos, end, number: number);
            _pos = end;
            return true;
        }
        if (config.BuiltinLiterals.TryGetValue(str, out Value? literal))
        {
            _current = NewToken(TokenKind.Const, str, startPos, end, constValue: literal);
            _pos = end;
            return true;
        }
        return false;
    }

    private bool IsNamedOp()
    {
        int startPos = _pos;
        int end = ScanWord(startPos, allowDot: false);
        if (end == startPos)
        {
            return false;
        }
        string str = _expression[startPos..end];

        // The named operator 'not in' is two words separated by a single space;
        // detect it by peeking past the first word.
        if (str == "not" && StartsWithAt(startPos, "not in"))
        {
            str = "not in";
        }

        if (config.IsOperatorEnabled(str) && config.IsNamedOperator(str))
        {
            _current = NewToken(TokenKind.Op, str, startPos, startPos + str.Length);
            _pos += str.Length;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Scans a word: first char must be a letter, subsequent chars may be
    /// letters, digits, '_', or (when <paramref name="allowDot"/>) '.'.
    /// Returns <paramref name="startPos"/> if no word was matched.
    /// </summary>
    private int ScanWord(int startPos, bool allowDot)
    {
        int pos = startPos;
        if (pos >= _expression.Length || !IsLetter(_expression[pos]))
        {
            return startPos;
        }
        pos++;
        while (pos < _expression.Length)
        {
            char c = _expression[pos];
            bool isContinuation =
                IsLetter(c)
                || c == '_'
                || (c >= '0' && c <= '9')
                || (allowDot && c == '.');
            if (!isContinuation)
            {
                break;
            }
            pos++;
        }
        return pos;
    }

    /// <summary>
    /// Identifier grammar:
    ///   Name := Start (Continue)*
    ///   Start := letter | '_' | '$' ('$')?
    ///   Continue := letter | digit | '_'
    /// A leading '$' does not by itself make the token an identifier; at
    /// least one letter (or leading '_') must appear, so bare '$' / '$$' are
    /// rejected but '$x' / '$$x' / '_' are accepted.
    /// </summary>
    private bool IsName()
    {
        int startPos = _pos;
        if (startPos >= _expression.Length)
        {
            return false;
        }

        int pos = startPos;
        bool hasLetter;
        bool leadingDollar;
        char first = _expression[pos];
        if (IsLetter(first) || first == '_')
        {
            hasLetter = true;
            leadingDollar = false;
        }
        else if (first == '$')
        {
            hasLetter = false;
            leadingDollar = true;
        }
        else
        {
            return false;
        }
        pos++;

        // Optional second '$' after a leading '$' - allows '$$name'.
        if (leadingDollar && pos < _expression.Length && _expression[pos] == '$')
        {
            pos++;
        }

        while (pos < _expression.Length)
        {
            char c = _expression[pos];
            if (IsLetter(c))
            {
                hasLetter = true;
            }
            else if (hasLetter && (c == '_' || (c >= '0' && c <= '9')))
            {
                // valid continuation
            }
            else
            {
                break;
            }
            pos++;
        }

        if (!hasLetter)
        {
            return false;
        }

        string str = _expression[startPos..pos];
        TokenKind kind = config.Keywords.Contains(str) ? TokenKind.Keyword : TokenKind.Name;
        _current = NewToken(kind, str, startPos, pos);
        _pos = pos;
        return true;
    }

    // ---------------- operators ----------------

    // Single-char operator glyphs that are always their own token when enabled.
    // '?' / '=' / '<' / '>' / '!' / '|' are also single-char ops but they
    // double as the first char of a compound and are covered via TryOneCharOp.
    // '&' is intentionally excluded: a bare '&' is not a valid operator.
    private static readonly HashSet<char> CompoundAmbiguousOps =
    [
        '?',
        '=',
        '<',
        '>',
        '!',
        '|',
    ];

    /// <summary>
    /// Tokenises the operator starting at <c>_pos</c> using a longest-match
    /// rule: try the 3-char form ('...') first, then any 2-char compound,
    /// then a 1-char op, then the keyword-op 'as'. Every non-'...' form is
    /// gated by <see cref="ParserConfig.IsOperatorEnabled"/>; a disabled form
    /// does not fall back to a shorter form (matches the original behaviour).
    /// </summary>
    private bool IsOperator()
    {
        int startPos = _pos;

        if (StartsWithAt(startPos, "..."))
        {
            return EmitOperatorUnchecked(startPos, "...", 3);
        }

        string? compound = TryTwoCharOperator(CharAt(startPos), CharAt(startPos + 1));
        if (compound is not null)
        {
            return EmitOperator(startPos, compound, 2);
        }

        string? single = TryOneCharOperator(CharAt(startPos));
        if (single is not null)
        {
            return EmitOperator(startPos, single, 1);
        }

        // 'as' keyword-operator; requires trailing whitespace so it doesn't
        // swallow the prefix of identifiers like 'aspect'.
        if (StartsWithAt(startPos, "as "))
        {
            return EmitOperator(startPos, "as", 2);
        }

        return false;
    }

    private static string? TryTwoCharOperator(char first, char second) =>
        (first, second) switch
        {
            ('?', '?') => "??",
            ('>', '=') => ">=",
            ('<', '=') => "<=",
            ('=', '>') => "=>",
            ('=', '=') => "==",
            ('!', '=') => "!=",
            ('|', '|') => "||",
            ('&', '&') => "&&",
            _ => null,
        };

    private static string? TryOneCharOperator(char c)
    {
        if (SingleCharOperators.Contains(c))
        {
            return c.ToString();
        }
        if (MultiplicationSymbols.Contains(c))
        {
            return "*";
        }
        if (CompoundAmbiguousOps.Contains(c))
        {
            return c.ToString();
        }
        return null;
    }

    private bool EmitOperator(int startPos, string text, int length)
    {
        if (!config.IsOperatorEnabled(text))
        {
            return false;
        }
        return EmitOperatorUnchecked(startPos, text, length);
    }

    private bool EmitOperatorUnchecked(int startPos, string text, int length)
    {
        _current = NewToken(TokenKind.Op, text, startPos, startPos + length);
        _pos = startPos + length;
        return true;
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
