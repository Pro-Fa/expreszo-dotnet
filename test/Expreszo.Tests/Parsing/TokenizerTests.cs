using Expreszo.Parsing;

namespace Expreszo.Tests.Parsing;

public class TokenizerTests
{
    private static Token[] Tokenize(string expression)
    {
        var tokenizer = new Tokenizer(ParserConfig.Default, expression);
        var tokens = new List<Token>();
        while (true)
        {
            var t = tokenizer.Next();
            tokens.Add(t);
            if (t.Kind == TokenKind.Eof)
            {
                break;
            }
        }
        return [.. tokens];
    }

    // ---------- numbers ----------

    [Test]
    [Arguments("0", 0d)]
    [Arguments("1", 1d)]
    [Arguments("42", 42d)]
    [Arguments("3.14", 3.14)]
    [Arguments(".5", 0.5)]
    [Arguments("1e10", 1e10)]
    [Arguments("1E+10", 1e10)]
    [Arguments("1.5e-3", 1.5e-3)]
    [Arguments("0.1e2", 10d)]
    public async Task Tokenizes_decimal_numbers(string input, double expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens.Length).IsEqualTo(2);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[0].Number).IsEqualTo(expected);
    }

    [Test]
    [Arguments("0xFF", 255d)]
    [Arguments("0x1A", 26d)]
    [Arguments("0xffff", 65535d)]
    [Arguments("0b10", 2d)]
    [Arguments("0b1111", 15d)]
    public async Task Tokenizes_radix_integers(string input, double expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[0].Number).IsEqualTo(expected);
    }

    [Test]
    public async Task Invalid_radix_integer_falls_back_to_regular_number()
    {
        // "0x" without digits is not a valid hex — falls through to regular number (just 0),
        // leaving "x" as a name.
        var tokens = Tokenize("0x");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[0].Number).IsEqualTo(0d);
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Name);
        await Assert.That(tokens[1].Text).IsEqualTo("x");
    }

    // ---------- strings ----------

    [Test]
    [Arguments("\"hello\"", "hello")]
    [Arguments("'hello'", "hello")]
    [Arguments("\"\"", "")]
    [Arguments("'with spaces'", "with spaces")]
    public async Task Tokenizes_strings(string input, string expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.String);
        await Assert.That(tokens[0].Text).IsEqualTo(expected);
    }

    [Test]
    [Arguments("\"a\\nb\"", "a\nb")]
    [Arguments("\"a\\tb\"", "a\tb")]
    [Arguments("\"\\\\n\"", "\\n")]
    [Arguments("\"\\\"\"", "\"")]
    [Arguments("\"a\\u0041b\"", "aAb")]
    public async Task Unescapes_standard_escape_sequences(string input, string expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Text).IsEqualTo(expected);
    }

    [Test]
    public async Task Handles_escaped_backslash_before_closing_quote()
    {
        // "a\\" contains backslash + escaped backslash ; closing quote is not escaped
        var tokens = Tokenize("\"a\\\\\"");
        await Assert.That(tokens[0].Text).IsEqualTo("a\\");
    }

    [Test]
    public async Task Illegal_escape_sequence_throws()
    {
        await Assert.That(() => Tokenize("\"a\\z\"")).Throws<ParseException>();
    }

    // ---------- identifiers / keywords / constants ----------

    [Test]
    [Arguments("foo")]
    [Arguments("_foo")]
    [Arguments("$foo")]
    [Arguments("$$foo")]
    [Arguments("foo_bar")]
    [Arguments("foo123")]
    public async Task Tokenizes_identifiers(string input)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Name);
        await Assert.That(tokens[0].Text).IsEqualTo(input);
    }

    [Test]
    [Arguments("case")]
    [Arguments("when")]
    [Arguments("then")]
    [Arguments("else")]
    [Arguments("end")]
    public async Task Tokenizes_keywords(string input)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Keyword);
        await Assert.That(tokens[0].Text).IsEqualTo(input);
    }

    [Test]
    public async Task Tokenizes_true_as_const()
    {
        var tokens = Tokenize("true");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Const);
        await Assert.That(tokens[0].Const).IsSameReferenceAs(Value.Boolean.True);
    }

    [Test]
    public async Task Tokenizes_false_as_const()
    {
        var tokens = Tokenize("false");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Const);
        await Assert.That(tokens[0].Const).IsSameReferenceAs(Value.Boolean.False);
    }

    [Test]
    public async Task Tokenizes_null_as_const()
    {
        var tokens = Tokenize("null");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Const);
        await Assert.That(tokens[0].Const).IsSameReferenceAs(Value.Null.Instance);
    }

    [Test]
    public async Task Tokenizes_PI_and_E_as_numeric_constants()
    {
        var tokens = Tokenize("PI E");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[0].Number).IsEqualTo(Math.PI);
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[1].Number).IsEqualTo(Math.E);
    }

    // ---------- operators ----------

    [Test]
    [Arguments("+", "+")]
    [Arguments("-", "-")]
    [Arguments("*", "*")]
    [Arguments("/", "/")]
    [Arguments("%", "%")]
    [Arguments("^", "^")]
    [Arguments(":", ":")]
    [Arguments(".", ".")]
    [Arguments("?", "?")]
    public async Task Tokenizes_single_char_operators(string input, string expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[0].Text).IsEqualTo(expected);
    }

    [Test]
    [Arguments("==", "==")]
    [Arguments("!=", "!=")]
    [Arguments(">=", ">=")]
    [Arguments("<=", "<=")]
    [Arguments("&&", "&&")]
    [Arguments("||", "||")]
    [Arguments("??", "??")]
    [Arguments("=>", "=>")]
    [Arguments("...", "...")]
    public async Task Tokenizes_multi_char_operators(string input, string expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[0].Text).IsEqualTo(expected);
    }

    [Test]
    [Arguments("and")]
    [Arguments("or")]
    [Arguments("in")]
    public async Task Tokenizes_named_binary_operators(string input)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[0].Text).IsEqualTo(input);
    }

    [Test]
    public async Task Tokenizes_not_in_as_compound_operator()
    {
        var tokens = Tokenize("x not in y");
        await Assert.That(tokens[0].Text).IsEqualTo("x");
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[1].Text).IsEqualTo("not in");
        await Assert.That(tokens[2].Text).IsEqualTo("y");
    }

    [Test]
    public async Task Tokenizes_bare_not()
    {
        var tokens = Tokenize("not x");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[0].Text).IsEqualTo("not");
    }

    [Test]
    public async Task Tokenizes_as_keyword_operator_with_trailing_space()
    {
        var tokens = Tokenize("x as number");
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[1].Text).IsEqualTo("as");
    }

    [Test]
    [Arguments("∙", "*")]
    [Arguments("•", "*")]
    public async Task Unicode_multiplication_symbols_become_star(string input, string expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Op);
        await Assert.That(tokens[0].Text).IsEqualTo(expected);
    }

    [Test]
    public async Task Tokenizes_math_functions_as_op_tokens()
    {
        var tokens = Tokenize("sin cos sqrt length");
        foreach (var t in tokens.Take(4))
        {
            await Assert.That(t.Kind).IsEqualTo(TokenKind.Op);
        }
    }

    [Test]
    public async Task Ampersand_without_second_ampersand_is_illegal()
    {
        await Assert.That(() => Tokenize("&x")).Throws<ParseException>();
    }

    // ---------- punctuation & grouping ----------

    [Test]
    public async Task Tokenizes_parens_brackets_braces_comma_semicolon()
    {
        var tokens = Tokenize("() [] {} , ;");
        var expected = new (TokenKind Kind, string Text)[]
        {
            (TokenKind.Paren, "("),
            (TokenKind.Paren, ")"),
            (TokenKind.Bracket, "["),
            (TokenKind.Bracket, "]"),
            (TokenKind.Brace, "{"),
            (TokenKind.Brace, "}"),
            (TokenKind.Comma, ","),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Eof, "EOF"),
        };
        await Assert.That(tokens.Length).IsEqualTo(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            await Assert.That(tokens[i].Kind).IsEqualTo(expected[i].Kind);
            await Assert.That(tokens[i].Text).IsEqualTo(expected[i].Text);
        }
    }

    // ---------- comments & whitespace ----------

    [Test]
    public async Task Skips_block_comments()
    {
        var tokens = Tokenize("1 /* inline */ + /* multi\nline */ 2");
        await Assert.That(tokens[0].Number).IsEqualTo(1d);
        await Assert.That(tokens[1].Text).IsEqualTo("+");
        await Assert.That(tokens[2].Number).IsEqualTo(2d);
    }

    [Test]
    public async Task Skips_line_comments()
    {
        var tokens = Tokenize("1 // ignored\n+ 2");
        await Assert.That(tokens[0].Number).IsEqualTo(1d);
        await Assert.That(tokens[1].Text).IsEqualTo("+");
        await Assert.That(tokens[2].Number).IsEqualTo(2d);
    }

    [Test]
    public async Task Eof_token_is_emitted_on_empty_input()
    {
        var tokens = Tokenize("");
        await Assert.That(tokens.Length).IsEqualTo(1);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task Whitespace_is_consumed_silently()
    {
        var tokens = Tokenize("   1   +   2   ");
        await Assert.That(tokens.Length).IsEqualTo(4);
        await Assert.That(tokens[0].Number).IsEqualTo(1d);
        await Assert.That(tokens[1].Text).IsEqualTo("+");
        await Assert.That(tokens[2].Number).IsEqualTo(2d);
        await Assert.That(tokens[3].Kind).IsEqualTo(TokenKind.Eof);
    }

    // ---------- composition ----------

    [Test]
    public async Task Tokenizes_a_complete_expression()
    {
        var tokens = Tokenize("max(x, 2 * PI) + 0xFF");
        await Assert.That(tokens[0].Text).IsEqualTo("max");
        await Assert.That(tokens[1].Text).IsEqualTo("(");
        await Assert.That(tokens[2].Text).IsEqualTo("x");
        await Assert.That(tokens[3].Text).IsEqualTo(",");
        await Assert.That(tokens[4].Number).IsEqualTo(2d);
        await Assert.That(tokens[5].Text).IsEqualTo("*");
        await Assert.That(tokens[6].Number).IsEqualTo(Math.PI);
        await Assert.That(tokens[7].Text).IsEqualTo(")");
        await Assert.That(tokens[8].Text).IsEqualTo("+");
        await Assert.That(tokens[9].Number).IsEqualTo(255d);
    }

    [Test]
    public async Task Unknown_character_raises_ParseException()
    {
        await Assert.That(() => Tokenize("@")).Throws<ParseException>();
    }

    [Test]
    public async Task Index_and_End_cover_token_span()
    {
        var tokens = Tokenize("  42 ");
        await Assert.That(tokens[0].Index).IsEqualTo(2);
        await Assert.That(tokens[0].End).IsEqualTo(4);
    }
}
