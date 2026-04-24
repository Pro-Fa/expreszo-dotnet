namespace Expreszo.LanguageServer.Tests;

public class LineIndexTests
{
    [Test]
    public async Task Single_line_offsets_map_one_to_one()
    {
        var idx = new LineIndex("1 + 2");

        (int line, int character) = idx.OffsetToPosition(4);

        await Assert.That(line).IsEqualTo(0);
        await Assert.That(character).IsEqualTo(4);
    }

    [Test]
    public async Task Lf_newlines_produce_correct_line_starts()
    {
        var idx = new LineIndex("a\nbb\nccc");

        await Assert.That(idx.OffsetToPosition(0)).IsEqualTo((0, 0));
        await Assert.That(idx.OffsetToPosition(2)).IsEqualTo((1, 0));
        await Assert.That(idx.OffsetToPosition(5)).IsEqualTo((2, 0));
        await Assert.That(idx.OffsetToPosition(7)).IsEqualTo((2, 2));
    }

    [Test]
    public async Task Crlf_newlines_collapse_to_single_break()
    {
        var idx = new LineIndex("a\r\nb");

        await Assert.That(idx.OffsetToPosition(3)).IsEqualTo((1, 0));
    }

    [Test]
    public async Task Position_to_offset_round_trips_through_offset_to_position()
    {
        const string text = "foo\nbar\nbaz";
        var idx = new LineIndex(text);

        for (int offset = 0; offset <= text.Length; offset++)
        {
            (int line, int ch) = idx.OffsetToPosition(offset);
            int back = idx.PositionToOffset(line, ch);
            await Assert.That(back).IsEqualTo(offset);
        }
    }

    [Test]
    public async Task Out_of_range_offsets_clamp_instead_of_throwing()
    {
        var idx = new LineIndex("abc");

        await Assert.That(idx.OffsetToPosition(-1)).IsEqualTo((0, 0));
        await Assert.That(idx.OffsetToPosition(9999)).IsEqualTo((0, 3));
    }
}
