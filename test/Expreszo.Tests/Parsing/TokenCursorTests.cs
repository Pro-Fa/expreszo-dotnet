using Expreszo.Parsing;

namespace Expreszo.Tests.Parsing;

public class TokenCursorTests
{
    private static TokenCursor CursorFor(string expression) =>
        TokenCursor.From(ParserConfig.Default, expression);

    [Test]
    public async Task Cursor_starts_at_first_token()
    {
        // Arrange

        // Act
        TokenCursor c = CursorFor("1 + 2");

        // Assert
        await Assert.That(c.Peek().Number).IsEqualTo(1d);
        await Assert.That(c.Index).IsEqualTo(0);
    }

    [Test]
    public async Task Advance_returns_a_new_cursor_without_mutating_the_original()
    {
        // Arrange
        TokenCursor c = CursorFor("1 + 2");

        // Act
        TokenCursor c2 = c.Advance();

        // Assert
        await Assert.That(c.Peek().Number).IsEqualTo(1d);
        await Assert.That(c2.Peek().Text).IsEqualTo("+");
        await Assert.That(c).IsNotSameReferenceAs(c2);
    }

    [Test]
    public async Task PeekAt_reads_future_tokens_without_advancing()
    {
        // Arrange
        TokenCursor c = CursorFor("1 + 2");

        // Act & Assert
        await Assert.That(c.PeekAt(0).Number).IsEqualTo(1d);
        await Assert.That(c.PeekAt(1).Text).IsEqualTo("+");
        await Assert.That(c.PeekAt(2).Number).IsEqualTo(2d);
    }

    [Test]
    public async Task PeekAt_past_the_end_clamps_to_Eof()
    {
        // Arrange
        TokenCursor c = CursorFor("1");

        // Act
        Token t = c.PeekAt(10);

        // Assert
        await Assert.That(t.Kind).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task AtEnd_is_true_once_cursor_reaches_Eof()
    {
        // Arrange
        TokenCursor c = CursorFor("1");

        // Act
        TokenCursor c2 = c.Advance();

        // Assert
        await Assert.That(c.AtEnd).IsFalse();
        await Assert.That(c2.AtEnd).IsTrue();
    }

    [Test]
    public async Task Advance_past_Eof_returns_same_cursor()
    {
        // Arrange
        TokenCursor c = CursorFor("");

        // Act
        TokenCursor c2 = c.Advance();

        // Assert
        await Assert.That(c).IsSameReferenceAs(c2);
    }

    [Test]
    public async Task Check_matches_kind_and_optional_text()
    {
        // Arrange
        TokenCursor c = CursorFor("+ 1");

        // Act & Assert
        await Assert.That(c.Check(TokenKind.Op)).IsTrue();
        await Assert.That(c.Check(TokenKind.Op, "+")).IsTrue();
        await Assert.That(c.Check(TokenKind.Op, "-")).IsFalse();
        await Assert.That(c.Check(TokenKind.Number)).IsFalse();
    }

    [Test]
    public async Task Match_advances_when_matched_and_returns_null_otherwise()
    {
        // Arrange
        TokenCursor c = CursorFor("1");

        // Act
        (Token Token, TokenCursor Next)? matched = c.Match(TokenKind.Number);
        (Token Token, TokenCursor Next)? notMatched = c.Match(TokenKind.Op);

        // Assert
        await Assert.That(matched).IsNotNull();
        await Assert.That(matched!.Value.Token.Number).IsEqualTo(1d);
        await Assert.That(matched.Value.Next.AtEnd).IsTrue();
        await Assert.That(notMatched).IsNull();
    }

    [Test]
    public async Task PreviousEnd_tracks_consumed_token_end_offsets()
    {
        // Arrange
        TokenCursor c = CursorFor("abc + 42");

        // Act
        TokenCursor c2 = c.Advance();

        // Assert
        // previous token was "abc" (indices 0..3)
        await Assert.That(c2.PreviousEnd()).IsEqualTo(3);
    }

    [Test]
    public async Task Backtracking_is_a_single_assignment()
    {
        // Arrange
        TokenCursor c = CursorFor("1 + 2");
        TokenCursor saved = c;

        // Act
        TokenCursor moved = c.Advance().Advance();

        // Assert
        await Assert.That(moved.Peek().Number).IsEqualTo(2d);
        // Restore is just using the saved reference again.
        await Assert.That(saved.Peek().Number).IsEqualTo(1d);
    }

    [Test]
    public async Task Coordinates_are_1_based_line_and_column()
    {
        // Arrange
        TokenCursor c = CursorFor("foo\nbar");

        // Act
        ErrorPosition coord = c.GetCoordinates();
        TokenCursor c2 = c.Advance();
        ErrorPosition coord2 = c2.GetCoordinates();

        // Assert
        await Assert.That(coord.Line).IsEqualTo(1);
        await Assert.That(coord.Column).IsEqualTo(1);
        // advance past foo and newline + whitespace handled by tokenizer
        await Assert.That(coord2.Line).IsEqualTo(2);
    }
}
