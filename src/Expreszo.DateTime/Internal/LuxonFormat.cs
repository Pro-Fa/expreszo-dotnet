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

            // Quoted literal. Luxon escapes a literal single quote as a doubled
            // '' (inside or outside a quoted run); a lone ' closes the run.
            // Emit each literal char backslash-escaped so any character —
            // including quotes and letters — passes through .NET verbatim.
            if (c == '\'')
            {
                i++; // opening quote
                while (i < luxon.Length)
                {
                    if (luxon[i] == '\'')
                    {
                        if (i + 1 < luxon.Length && luxon[i + 1] == '\'')
                        {
                            sb.Append("\\'"); // escaped literal quote
                            i += 2;
                            continue;
                        }
                        i++; // closing quote
                        break;
                    }
                    sb.Append('\\').Append(luxon[i]);
                    i++;
                }
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
