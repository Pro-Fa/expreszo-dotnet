using Expreszo.Parsing;

namespace Expreszo.Tests.Parsing;

public class TokenCursorTests
{
    private static TokenCursor CursorFor(string expression) =>
        TokenCursor.From(TokenizerConfig.Default, expression);

    [Test]
    public async Task Cursor_starts_at_first_token()
    {
        var c = CursorFor("1 + 2");
        await Assert.That(c.Peek().Number).IsEqualTo(1d);
        await Assert.That(c.Index).IsEqualTo(0);
    }

    [Test]
    public async Task Advance_returns_a_new_cursor_without_mutating_the_original()
    {
        var c = CursorFor("1 + 2");
        var c2 = c.Advance();
        await Assert.That(c.Peek().Number).IsEqualTo(1d);
        await Assert.That(c2.Peek().Text).IsEqualTo("+");
        await Assert.That(c).IsNotSameReferenceAs(c2);
    }

    [Test]
    public async Task PeekAt_reads_future_tokens_without_advancing()
    {
        var c = CursorFor("1 + 2");
        await Assert.That(c.PeekAt(0).Number).IsEqualTo(1d);
        await Assert.That(c.PeekAt(1).Text).IsEqualTo("+");
        await Assert.That(c.PeekAt(2).Number).IsEqualTo(2d);
    }

    [Test]
    public async Task PeekAt_past_the_end_clamps_to_Eof()
    {
        var c = CursorFor("1");
        await Assert.That(c.PeekAt(10).Kind).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task AtEnd_is_true_once_cursor_reaches_Eof()
    {
        var c = CursorFor("1");
        await Assert.That(c.AtEnd).IsFalse();
        var c2 = c.Advance();
        await Assert.That(c2.AtEnd).IsTrue();
    }

    [Test]
    public async Task Advance_past_Eof_returns_same_cursor()
    {
        var c = CursorFor("");
        var c2 = c.Advance();
        await Assert.That(c).IsSameReferenceAs(c2);
    }

    [Test]
    public async Task Check_matches_kind_and_optional_text()
    {
        var c = CursorFor("+ 1");
        await Assert.That(c.Check(TokenKind.Op)).IsTrue();
        await Assert.That(c.Check(TokenKind.Op, "+")).IsTrue();
        await Assert.That(c.Check(TokenKind.Op, "-")).IsFalse();
        await Assert.That(c.Check(TokenKind.Number)).IsFalse();
    }

    [Test]
    public async Task Match_advances_when_matched_and_returns_null_otherwise()
    {
        var c = CursorFor("1");
        var matched = c.Match(TokenKind.Number);
        await Assert.That(matched).IsNotNull();
        await Assert.That(matched!.Value.Token.Number).IsEqualTo(1d);
        await Assert.That(matched.Value.Next.AtEnd).IsTrue();

        var notMatched = c.Match(TokenKind.Op);
        await Assert.That(notMatched).IsNull();
    }

    [Test]
    public async Task PreviousEnd_tracks_consumed_token_end_offsets()
    {
        var c = CursorFor("abc + 42");
        var c2 = c.Advance();
        // previous token was "abc" (indices 0..3)
        await Assert.That(c2.PreviousEnd()).IsEqualTo(3);
    }

    [Test]
    public async Task Backtracking_is_a_single_assignment()
    {
        var c = CursorFor("1 + 2");
        var saved = c;
        var moved = c.Advance().Advance();
        await Assert.That(moved.Peek().Number).IsEqualTo(2d);
        // Restore is just using the saved reference again.
        await Assert.That(saved.Peek().Number).IsEqualTo(1d);
    }

    [Test]
    public async Task Coordinates_are_1_based_line_and_column()
    {
        var c = CursorFor("foo\nbar");
        var coord = c.GetCoordinates();
        await Assert.That(coord.Line).IsEqualTo(1);
        await Assert.That(coord.Column).IsEqualTo(1);

        // advance past foo and newline + whitespace handled by tokenizer
        var c2 = c.Advance();
        var coord2 = c2.GetCoordinates();
        await Assert.That(coord2.Line).IsEqualTo(2);
    }
}
