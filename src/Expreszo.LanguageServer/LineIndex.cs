namespace Expreszo.LanguageServer;

/// <summary>
/// Precomputes newline offsets for a source document and converts between
/// absolute character offsets (as used by <c>TextSpan</c>) and LSP
/// <c>(line, character)</c> positions.
/// </summary>
/// <remarks>
/// LSP line/character are both 0-based and count UTF-16 code units. The
/// default LSP position encoding is <c>utf-16</c>, which matches how .NET
/// <see cref="string"/> indices work, so each code-unit offset in the source
/// maps 1:1 to an LSP character column.
/// </remarks>
internal sealed class LineIndex
{
    private readonly string _text;
    private readonly int[] _lineStarts;

    public LineIndex(string text)
    {
        _text = text ?? string.Empty;
        _lineStarts = BuildLineStarts(_text);
    }

    public int LineCount => _lineStarts.Length;

    /// <summary>Converts a 0-based character offset into a 0-based (line, character) pair.</summary>
    public (int Line, int Character) OffsetToPosition(int offset)
    {
        if (offset < 0)
        {
            offset = 0;
        }

        if (offset > _text.Length)
        {
            offset = _text.Length;
        }

        int line = BinarySearchLine(offset);
        int character = offset - _lineStarts[line];
        return (line, character);
    }

    /// <summary>Converts a 0-based (line, character) pair into a 0-based character offset.</summary>
    public int PositionToOffset(int line, int character)
    {
        if (line < 0)
        {
            return 0;
        }

        if (line >= _lineStarts.Length)
        {
            return _text.Length;
        }

        int lineStart = _lineStarts[line];
        int lineEnd = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : _text.Length;

        // Strip a trailing CR if the line was terminated by CRLF.
        if (lineEnd > lineStart && _text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        int offset = lineStart + character;
        return offset < lineStart ? lineStart : offset > lineEnd ? lineEnd : offset;
    }

    private int BinarySearchLine(int offset)
    {
        int lo = 0;
        int hi = _lineStarts.Length - 1;

        while (lo < hi)
        {
            int mid = lo + ((hi - lo + 1) / 2);
            if (_lineStarts[mid] <= offset)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return lo;
    }

    private static int[] BuildLineStarts(string text)
    {
        List<int> starts = [0];

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                starts.Add(i + 1);
            }
            else if (c == '\r')
            {
                // Treat CR, LF, and CRLF as a single line break.
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
        }

        return [.. starts];
    }
}
