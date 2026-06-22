using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Expreszo.Errors;

namespace Expreszo.Builtins;

/// <summary>Registers the string-category functions.</summary>
internal static class StringPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        b.AddFunction(
            "length",
            OperatorTableBuilder.Sync(args =>
            {
                return args[0] switch
                {
                    Value.Undefined => Value.Undefined.Instance,
                    Value.Null => Value.Number.Of(0),
                    Value.String s => Value.Number.Of(s.V.Length),
                    Value.Array a => Value.Number.Of(a.Items.Length),
                    _ => Value.Undefined.Instance,
                };
            })
        );

        b.AddFunction(
            "isEmpty",
            OperatorTableBuilder.Sync(args =>
            {
                return args[0] switch
                {
                    Value.Null => Value.Boolean.True,
                    Value.Undefined => Value.Undefined.Instance,
                    Value.String s => Value.Boolean.Of(s.V.Length == 0),
                    _ => Value.Undefined.Instance,
                };
            })
        );

        b.AddFunction(
            "contains",
            OperatorTableBuilder.Sync(args =>
            {
                (Value haystack, Value needle) = (args[0], args[1]);
                if (haystack is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                if (haystack is Value.String s)
                {
                    if (needle is Value.String ns)
                    {
                        return Value.Boolean.Of(s.V.Contains(ns.V, StringComparison.Ordinal));
                    }

                    return Value.Boolean.Of(
                        s.V.Contains(CorePreset.ToStringValue(needle), StringComparison.Ordinal)
                    );
                }
                if (haystack is Value.Array a)
                {
                    foreach (Value item in a.Items)
                    {
                        if (CorePreset.StrictEquals(item, needle))
                        {
                            return Value.Boolean.True;
                        }
                    }
                    return Value.Boolean.False;
                }
                return Value.Boolean.False;
            })
        );

        StrStrBool(b, "startsWith", (s, t) => s.StartsWith(t, StringComparison.Ordinal));
        StrStrBool(b, "endsWith", (s, t) => s.EndsWith(t, StringComparison.Ordinal));

        b.AddFunction(
            "searchCount",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s || args[1] is not Value.String n)
                {
                    return Value.Undefined.Instance;
                }

                if (n.V.Length == 0)
                {
                    return Value.Number.Of(0);
                }

                int count = 0;
                int idx = 0;
                while ((idx = s.V.IndexOf(n.V, idx, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    idx += n.V.Length;
                }
                return Value.Number.Of(count);
            })
        );

        b.AddFunction(
            "trim",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                if (args.Length >= 2 && args[1] is Value.String chars)
                {
                    return new Value.String(s.V.Trim(chars.V.ToCharArray()));
                }
                return new Value.String(s.V.Trim());
            })
        );

        StrToStr(b, "toUpper", s => s.ToUpperInvariant());
        StrToStr(b, "toLower", s => s.ToLowerInvariant());

        b.AddFunction(
            "toTitle",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                TextInfo ti = CultureInfo.InvariantCulture.TextInfo;
                return new Value.String(ti.ToTitleCase(s.V.ToLowerInvariant()));
            })
        );

        b.AddFunction(
            "split",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s || args[1] is not Value.String d)
                {
                    return Value.Undefined.Instance;
                }

                Value[] parts =
                    d.V.Length == 0
                        ? s.V.Select(c => (Value)new Value.String(c.ToString())).ToArray()
                        : s
                            .V.Split(d.V, StringSplitOptions.None)
                            .Select(p => (Value)new Value.String(p))
                            .ToArray();
                return new Value.Array([.. parts]);
            })
        );

        b.AddFunction(
            "repeat",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                double n = CorePreset.ToNumber(args[1]);
                if (n < 0 || n != Math.Floor(n) || double.IsNaN(n) || double.IsInfinity(n))
                {
                    throw new EvaluationException(
                        "repeat requires a non-negative finite integer count"
                    );
                }

                int count = (int)n;
                long total = (long)s.V.Length * count;
                if (total > EvaluationLimits.MaxStringLength)
                {
                    throw new EvaluationException(
                        $"repeat result would exceed the {EvaluationLimits.MaxStringLength}-character limit"
                    );
                }

                return new Value.String(string.Concat(Enumerable.Repeat(s.V, count)));
            })
        );

        b.AddFunction(
            "reverse",
            OperatorTableBuilder.Sync(args =>
            {
                return args[0] switch
                {
                    Value.String s => new Value.String(new string(s.V.Reverse().ToArray())),
                    Value.Array a => new Value.Array([.. a.Items.Reverse()]),
                    _ => Value.Undefined.Instance,
                };
            })
        );

        b.AddFunction(
            "left",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                int n = (int)CorePreset.ToNumber(args[1]);
                return new Value.String(s.V[..Math.Min(n, s.V.Length)]);
            })
        );

        b.AddFunction(
            "right",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                int n = (int)CorePreset.ToNumber(args[1]);
                return new Value.String(s.V[Math.Max(0, s.V.Length - n)..]);
            })
        );

        b.AddFunction(
            "replace",
            OperatorTableBuilder.Sync(args =>
            {
                if (
                    args[0] is not Value.String s
                    || args[1] is not Value.String find
                    || args[2] is not Value.String repl
                )
                {
                    return Value.Undefined.Instance;
                }

                return new Value.String(s.V.Replace(find.V, repl.V, StringComparison.Ordinal));
            })
        );

        b.AddFunction(
            "replaceFirst",
            OperatorTableBuilder.Sync(args =>
            {
                if (
                    args[0] is not Value.String s
                    || args[1] is not Value.String find
                    || args[2] is not Value.String repl
                )
                {
                    return Value.Undefined.Instance;
                }

                int idx = s.V.IndexOf(find.V, StringComparison.Ordinal);
                if (idx < 0)
                {
                    return new Value.String(s.V);
                }

                return new Value.String(s.V[..idx] + repl.V + s.V[(idx + find.V.Length)..]);
            })
        );

        b.AddFunction(
            "naturalSort",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.Array arr)
                {
                    return Value.Undefined.Instance;
                }

                List<string> items = arr.Items.Select(v => v is Value.String s ? s.V : "").ToList();
                items.Sort(NaturalCompare);
                return new Value.Array([.. items.Select(s => (Value)new Value.String(s))]);
            })
        );

        b.AddFunction(
            "toNumber",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                if (
                    !double.TryParse(
                        s.V,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double n
                    )
                )
                {
                    throw new EvaluationException($"toNumber: cannot parse '{s.V}'");
                }
                return Value.Number.Of(n);
            })
        );

        b.AddFunction(
            "toBoolean",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                return s.V.ToLowerInvariant() switch
                {
                    "true" or "1" or "yes" or "on" => Value.Boolean.True,
                    "false" or "0" or "no" or "off" or "" => Value.Boolean.False,
                    _ => throw new EvaluationException($"toBoolean: cannot parse '{s.V}'"),
                };
            })
        );

        b.AddFunction(
            "padLeft",
            OperatorTableBuilder.Sync(args => Pad(args, padLeft: true, both: false))
        );
        b.AddFunction(
            "padRight",
            OperatorTableBuilder.Sync(args => Pad(args, padLeft: false, both: false))
        );
        b.AddFunction(
            "padBoth",
            OperatorTableBuilder.Sync(args => Pad(args, padLeft: false, both: true))
        );

        b.AddFunction(
            "slice",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is Value.String s)
                {
                    int start = NormalizeIdx((int)CorePreset.ToNumber(args[1]), s.V.Length);
                    int end =
                        args.Length >= 3 && args[2] is not Value.Undefined
                            ? NormalizeIdx((int)CorePreset.ToNumber(args[2]), s.V.Length)
                            : s.V.Length;
                    if (end < start)
                    {
                        end = start;
                    }

                    return new Value.String(s.V[start..end]);
                }
                if (args[0] is Value.Array a)
                {
                    int start = NormalizeIdx((int)CorePreset.ToNumber(args[1]), a.Items.Length);
                    int end =
                        args.Length >= 3 && args[2] is not Value.Undefined
                            ? NormalizeIdx((int)CorePreset.ToNumber(args[2]), a.Items.Length)
                            : a.Items.Length;
                    if (end < start)
                    {
                        end = start;
                    }

                    return new Value.Array([.. a.Items.AsSpan(start, end - start).ToArray()]);
                }
                return Value.Undefined.Instance;
            })
        );

        b.AddFunction(
            "urlEncode",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                return new Value.String(Uri.EscapeDataString(s.V));
            })
        );

        b.AddFunction(
            "base64Encode",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                return new Value.String(Convert.ToBase64String(Encoding.UTF8.GetBytes(s.V)));
            })
        );

        b.AddFunction(
            "base64Decode",
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                try
                {
                    return new Value.String(Encoding.UTF8.GetString(Convert.FromBase64String(s.V)));
                }
                catch (FormatException)
                {
                    throw new EvaluationException($"base64Decode: invalid input");
                }
            })
        );

        b.AddFunction(
            "regexMatches",
            OperatorTableBuilder.Sync(args =>
            {
                // Required: str, pattern. Missing or undefined -> undefined.
                if (args.Length < 2 || args[0] is Value.Undefined || args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                string s = RequireString("regexMatches", args[0], 0);
                string pattern = RequireString("regexMatches", args[1], 1);
                string? flags = OptionalFlags("regexMatches", args, 2);
                return Value.Boolean.Of(CompileRegex("regexMatches", pattern, flags).IsMatch(s));
            })
        );

        b.AddFunction(
            "regexExtract",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 2 || args[0] is Value.Undefined || args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                string s = RequireString("regexExtract", args[0], 0);
                string pattern = RequireString("regexExtract", args[1], 1);
                string? flags = OptionalFlags("regexExtract", args, 2);
                Match m = CompileRegex("regexExtract", pattern, flags).Match(s);
                if (!m.Success)
                {
                    return Value.Undefined.Instance;
                }

                // When the pattern has capture groups, return them; otherwise the
                // full match. Group 0 is the full match, so groups start at 1.
                if (m.Groups.Count > 1)
                {
                    Value[] groups = new Value[m.Groups.Count - 1];
                    for (int i = 1; i < m.Groups.Count; i++)
                    {
                        Group g = m.Groups[i];
                        groups[i - 1] = new Value.String(g.Success ? g.Value : "");
                    }

                    return new Value.Array([.. groups]);
                }

                return new Value.String(m.Value);
            })
        );

        b.AddFunction(
            "regexReplace",
            OperatorTableBuilder.Sync(args =>
            {
                if (
                    args.Length < 3
                    || args[0] is Value.Undefined
                    || args[1] is Value.Undefined
                    || args[2] is Value.Undefined
                )
                {
                    return Value.Undefined.Instance;
                }

                string s = RequireString("regexReplace", args[0], 0);
                string pattern = RequireString("regexReplace", args[1], 1);
                string replacement = RequireString("regexReplace", args[2], 2);
                string? flags = OptionalFlags("regexReplace", args, 3);
                Regex re = CompileRegex("regexReplace", pattern, flags);
                // Default (flags omitted) is a global replace; a supplied flag
                // string replaces only the first match unless it contains 'g'.
                bool global = flags is null || flags.Contains('g');
                return new Value.String(global ? re.Replace(s, replacement) : re.Replace(s, replacement, 1));
            })
        );

        b.AddFunction(
            "coalesce",
            OperatorTableBuilder.Sync(args =>
            {
                foreach (Value a in args)
                {
                    if (a is Value.Null or Value.Undefined)
                    {
                        continue;
                    }

                    if (a is Value.String s && s.V.Length == 0)
                    {
                        continue;
                    }

                    return a;
                }
                return Value.Undefined.Instance;
            })
        );
    }

    private static void StrStrBool(
        OperatorTableBuilder b,
        string name,
        Func<string, string, bool> fn
    )
    {
        b.AddFunction(
            name,
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s || args[1] is not Value.String t)
                {
                    return Value.Undefined.Instance;
                }

                return Value.Boolean.Of(fn(s.V, t.V));
            })
        );
    }

    /// <summary>Cap regex execution to guard against catastrophic backtracking (ReDoS).</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Returns the string payload of a required argument or throws (TS parity).</summary>
    private static string RequireString(string fn, Value v, int index)
    {
        if (v is Value.String s)
        {
            return s.V;
        }

        throw new ExpressionArgumentException(
            $"{fn}() expects a string as {Ordinal(index)} argument, got {v.TypeName()}",
            functionName: fn,
            argumentIndex: index,
            expectedType: "string",
            receivedType: v.TypeName()
        );
    }

    /// <summary>
    /// Reads an optional flags argument at <paramref name="index"/>. An omitted
    /// arg or an explicit <c>undefined</c> means "no flags"; any other non-string
    /// value throws (TS parity).
    /// </summary>
    private static string? OptionalFlags(string fn, Value[] args, int index)
    {
        if (args.Length <= index || args[index] is Value.Undefined)
        {
            return null;
        }

        return RequireString(fn, args[index], index);
    }

    /// <summary>Compiles a regex, mapping JS-style flag characters to <see cref="RegexOptions"/>.</summary>
    private static Regex CompileRegex(string fn, string pattern, string? flags)
    {
        RegexOptions opts = RegexOptions.None;
        if (flags is not null)
        {
            foreach (char f in flags)
            {
                opts |= f switch
                {
                    'i' => RegexOptions.IgnoreCase,
                    'm' => RegexOptions.Multiline,
                    's' => RegexOptions.Singleline,
                    // 'g' (global) is meaningful only for regexReplace, where the
                    // caller inspects it directly; it has no RegexOptions analogue.
                    'g' => RegexOptions.None,
                    _ => throw new EvaluationException($"{fn}(): invalid flags '{flags}'"),
                };
            }
        }

        try
        {
            return new Regex(pattern, opts, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new EvaluationException($"{fn}(): invalid pattern '{pattern}': {ex.Message}");
        }
    }

    /// <summary>Ordinal word for an argument position in error messages.</summary>
    private static string Ordinal(int index) =>
        index switch
        {
            0 => "first",
            1 => "second",
            2 => "third",
            3 => "fourth",
            _ => $"{index + 1}th",
        };

    private static void StrToStr(OperatorTableBuilder b, string name, Func<string, string> fn)
    {
        b.AddFunction(
            name,
            OperatorTableBuilder.Sync(args =>
            {
                if (args[0] is not Value.String s)
                {
                    return Value.Undefined.Instance;
                }

                return new Value.String(fn(s.V));
            })
        );
    }

    private static Value Pad(Value[] args, bool padLeft, bool both)
    {
        if (args[0] is not Value.String s)
        {
            return Value.Undefined.Instance;
        }

        double lenD = CorePreset.ToNumber(args[1]);
        if (double.IsNaN(lenD) || double.IsInfinity(lenD) || lenD < 0)
        {
            throw new EvaluationException("pad length must be a non-negative finite integer");
        }

        if (lenD > EvaluationLimits.MaxStringLength)
        {
            throw new EvaluationException(
                $"pad length exceeds the {EvaluationLimits.MaxStringLength}-character limit"
            );
        }

        int len = (int)lenD;
        char padChar =
            args.Length >= 3 && args[2] is Value.String pc && pc.V.Length > 0 ? pc.V[0] : ' ';
        if (s.V.Length >= len)
        {
            return s;
        }

        int total = len - s.V.Length;
        if (both)
        {
            int leftN = total / 2;
            int rightN = total - leftN;
            return new Value.String(new string(padChar, leftN) + s.V + new string(padChar, rightN));
        }
        return new Value.String(padLeft ? s.V.PadLeft(len, padChar) : s.V.PadRight(len, padChar));
    }

    private static int NormalizeIdx(int idx, int len)
    {
        if (idx < 0)
        {
            idx += len;
        }

        if (idx < 0)
        {
            idx = 0;
        }

        if (idx > len)
        {
            idx = len;
        }

        return idx;
    }

    private static int NaturalCompare(string a, string b)
    {
        int i = 0,
            j = 0;
        while (i < a.Length && j < b.Length)
        {
            char ca = a[i];
            char cb = b[j];
            if (char.IsDigit(ca) && char.IsDigit(cb))
            {
                int iStart = i;
                while (i < a.Length && char.IsDigit(a[i]))
                {
                    i++;
                }

                int jStart = j;
                while (j < b.Length && char.IsDigit(b[j]))
                {
                    j++;
                }

                long na = long.Parse(a.AsSpan(iStart, i - iStart), CultureInfo.InvariantCulture);
                long nb = long.Parse(b.AsSpan(jStart, j - jStart), CultureInfo.InvariantCulture);
                int cmp = na.CompareTo(nb);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else
            {
                int cmp = char.ToLowerInvariant(ca).CompareTo(char.ToLowerInvariant(cb));
                if (cmp != 0)
                {
                    return cmp;
                }

                i++;
                j++;
            }
        }
        return a.Length.CompareTo(b.Length);
    }
}
