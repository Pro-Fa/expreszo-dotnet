using System.Text;

namespace Expreszo.DateTimes.Internal;

/// <summary>
/// Translates a Luxon format-token string into the nearest .NET custom
/// date/time format string. Most tokens (<c>yyyy</c>, <c>MM</c>, <c>dd</c>,
/// <c>HH</c>, <c>mm</c>, <c>ss</c>, <c>MMM</c>, …) are identical between the two;
/// a handful are remapped. Quoted literals are passed through unchanged.
/// </summary>
internal static class LuxonFormat
{
    public static string Translate(string luxon)
    {
        var sb = new StringBuilder(luxon.Length + 4);
        int i = 0;
        while (i < luxon.Length)
        {
            char c = luxon[i];

            // Quoted literal: copy verbatim (single quotes mean the same thing
            // in .NET custom format strings).
            if (c == '\'')
            {
                int start = i++;
                while (i < luxon.Length && luxon[i] != '\'')
                {
                    i++;
                }
                if (i < luxon.Length)
                {
                    i++; // closing quote
                }
                sb.Append(luxon, start, i - start);
                continue;
            }

            if (!char.IsLetter(c))
            {
                sb.Append(c);
                i++;
                continue;
            }

            // Gather the run of identical letters.
            int runStart = i;
            while (i < luxon.Length && luxon[i] == c)
            {
                i++;
            }
            int len = i - runStart;
            sb.Append(MapRun(c, len));
        }
        return sb.ToString();
    }

    private static string MapRun(char letter, int len)
    {
        switch (letter)
        {
            // Identical between Luxon and .NET.
            case 'y':
            case 'M':
            case 'd':
            case 'H':
            case 'h':
            case 'm':
            case 's':
                return new string(letter, len);

            // Weekday names: Luxon EEE/EEEE -> .NET ddd/dddd.
            case 'E':
                return len >= 4 ? "dddd" : "ddd";

            // Meridiem: Luxon a -> .NET tt.
            case 'a':
                return "tt";

            // Fractional seconds: Luxon S/SSS -> .NET f/fff.
            case 'S':
                return new string('f', len);

            // Offset: Luxon ZZ.. -> .NET zzz; single Z -> K.
            case 'Z':
                return len >= 2 ? "zzz" : "K";

            // Anything else: emit as a quoted literal so .NET doesn't try to
            // interpret an unknown specifier.
            default:
                return "'" + new string(letter, len) + "'";
        }
    }
}
