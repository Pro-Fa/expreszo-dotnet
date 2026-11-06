using System.Globalization;
using System.Text.Json;
using Expreszo.Errors;
using Expreszo.Json;

namespace Expreszo.Builtins;

internal static class UtilityPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        // `if` is intercepted by the evaluator for lazy evaluation. This entry
        // exists so the function table lookup doesn't fail if someone passes
        // `if` as a value (e.g. `f = if; f(cond, a, b)`).
        b.AddFunction(
            "if",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 3)
                {
                    throw new ExpressionArgumentException("if requires 3 arguments", "if");
                }

                return args[0].IsTruthy() ? args[1] : args[2];
            })
        );

        b.AddFunction(
            "ipInRange",
            OperatorTableBuilder.Sync(args =>
            {
                if (args.Length < 2 || args[0] is Value.Undefined || args[1] is Value.Undefined)
                {
                    return Value.Undefined.Instance;
                }

                string ip = RequireString("ipInRange", args[0], 0);
                string cidr = RequireString("ipInRange", args[1], 1);

                int slash = cidr.IndexOf('/', StringComparison.Ordinal);
                if (slash == -1)
                {
                    throw new EvaluationException(
                        $"ipInRange(): invalid CIDR '{cidr}', expected form 'a.b.c.d/prefix'"
                    );
                }

                string network = cidr[..slash];
                string prefixStr = cidr[(slash + 1)..];
                if (
                    prefixStr.Length is 0 or > 2
                    || !int.TryParse(
                        prefixStr,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int prefix
                    )
                    || prefix > 32
                )
                {
                    throw new EvaluationException(
                        $"ipInRange(): invalid CIDR prefix in '{cidr}', must be 0-32"
                    );
                }

                uint ipInt = ParseIPv4("ipInRange", ip);
                uint netInt = ParseIPv4("ipInRange", network);
                // A /0 prefix matches everything; shifting a uint by 32 is undefined.
                uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
                return Value.Boolean.Of((ipInt & mask) == (netInt & mask));
            })
        );

        b.AddFunction("json", OperatorTableBuilder.Sync(ToJson));
        b.AddFunction("toJson", OperatorTableBuilder.Sync(ToJson));
        b.AddFunction("fromJson", OperatorTableBuilder.Sync(FromJson));
    }

    private const int MaxJsonDepth = 256;

    private static Value ToJson(Value[] args)
    {
        if (args.Length < 1 || args[0] is Value.Undefined)
        {
            return Value.Undefined.Instance;
        }

        try
        {
            return new Value.String(JsonBridge.ToJsonString(args[0]));
        }
        catch (InvalidOperationException)
        {
            // Thrown for Value.Function - not serialisable.
            return Value.Undefined.Instance;
        }
    }

    /// <summary>
    /// Parses a JSON string. Dangerous keys are dropped by <see cref="JsonBridge"/>,
    /// nesting is capped, and error messages never echo the input (TS parity).
    /// </summary>
    private static Value FromJson(Value[] args)
    {
        bool hasFallback = args.Length > 1;
        Value fallback = hasFallback ? args[1] : Value.Undefined.Instance;
        if (args.Length < 1 || args[0] is Value.Undefined)
        {
            return fallback;
        }

        string text = RequireString("fromJson", args[0], 0);
        if (ExceedsMaxJsonDepth(text))
        {
            if (hasFallback)
            {
                return fallback;
            }

            throw new EvaluationException(
                $"fromJson(): JSON exceeds the maximum nesting depth of {MaxJsonDepth}"
            );
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(
                text,
                new JsonDocumentOptions { MaxDepth = MaxJsonDepth + 1 }
            );
            return JsonBridge.FromJson(doc.RootElement);
        }
        catch (JsonException ex)
        {
            if (hasFallback)
            {
                return fallback;
            }

            // The inner exception is not attached: its message can quote the input.
            throw new EvaluationException(InvalidJsonMessage(ex));
        }
    }

    private static string InvalidJsonMessage(JsonException ex)
    {
        long line = ex.LineNumber ?? 0;
        long position = ex.BytePositionInLine ?? 0;
        return line == 0
            ? $"fromJson(): invalid JSON at position {position}"
            : $"fromJson(): invalid JSON at line {line + 1}, position {position}";
    }

    private static bool ExceedsMaxJsonDepth(string text)
    {
        int depth = 0;
        bool inString = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }
            }
            else if (c == '"')
            {
                inString = true;
            }
            else if (c is '[' or '{')
            {
                if (++depth > MaxJsonDepth)
                {
                    return true;
                }
            }
            else if (c is ']' or '}')
            {
                depth--;
            }
        }

        return false;
    }

    /// <summary>Returns the string payload of a required argument or throws (TS parity).</summary>
    private static string RequireString(string fn, Value v, int index)
    {
        if (v is Value.String s)
        {
            return s.V;
        }

        string ordinal = index == 0 ? "first" : "second";
        throw new ExpressionArgumentException(
            $"{fn}() expects a string as {ordinal} argument, got {v.TypeName()}",
            functionName: fn,
            argumentIndex: index,
            expectedType: "string",
            receivedType: v.TypeName()
        );
    }

    /// <summary>Parses a dotted-quad IPv4 address into an unsigned 32-bit integer.</summary>
    private static uint ParseIPv4(string fn, string ip)
    {
        string[] parts = ip.Split('.');
        if (parts.Length != 4)
        {
            throw new EvaluationException($"{fn}(): invalid IPv4 address '{ip}'");
        }

        uint result = 0;
        foreach (string part in parts)
        {
            // Reject empty, signs, and non-numeric octets; require a plain
            // decimal integer 0-255.
            if (
                part.Length is 0 or > 3
                || !int.TryParse(
                    part,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int octet
                )
                || octet > 255
            )
            {
                throw new EvaluationException($"{fn}(): invalid IPv4 address '{ip}'");
            }

            result = (result << 8) | (uint)octet;
        }

        return result;
    }
}

internal static class TypeCheckPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        Check(b, "isArray", v => v is Value.Array);
        Check(b, "isObject", v => v is Value.Object);
        Check(b, "isNumber", v => v is Value.Number);
        Check(b, "isString", v => v is Value.String);
        Check(b, "isBoolean", v => v is Value.Boolean);
        Check(b, "isNull", v => v is Value.Null);
        Check(b, "isUndefined", v => v is Value.Undefined);
        Check(b, "isFunction", v => v is Value.Function);
    }

    private static void Check(OperatorTableBuilder b, string name, Func<Value, bool> pred)
    {
        b.AddFunction(
            name,
            OperatorTableBuilder.Sync(args => Value.Boolean.Of(args.Length > 0 && pred(args[0])))
        );
    }
}
