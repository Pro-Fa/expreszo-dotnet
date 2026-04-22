using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Expreszo.Validation;

namespace Expreszo.Json;

/// <summary>
/// Conversions between <see cref="Value"/> and <see cref="System.Text.Json"/>
/// primitives. This is the I/O boundary of the library; everything above it
/// works in terms of <see cref="Value"/>.
/// </summary>
/// <remarks>
/// All conversions use AOT-safe <c>System.Text.Json</c> APIs
/// (<see cref="JsonElement"/>, <see cref="Utf8JsonWriter"/>). No reflection
/// based serialisation, no <c>JsonSerializer</c>, no <c>JsonSerializerContext</c>
/// - we walk the element tree manually so the library stays trim- and AOT-safe
/// without source generators.
/// </remarks>
public static class JsonBridge
{
    /// <summary>Converts a <see cref="JsonElement"/> to a <see cref="Value"/> recursively.</summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>Null</c> → <see cref="Value.Null"/></item>
    ///   <item><c>True</c> / <c>False</c> → <see cref="Value.Boolean"/></item>
    ///   <item><c>Number</c> → <see cref="Value.Number"/> (double; large integers lose precision, matching JS).</item>
    ///   <item><c>String</c> → <see cref="Value.String"/></item>
    ///   <item><c>Array</c> → <see cref="Value.Array"/></item>
    ///   <item><c>Object</c> → <see cref="Value.Object"/> (last-wins for duplicate keys).</item>
    ///   <item><c>Undefined</c> → <see cref="Value.Undefined"/> (only produced by a default <c>JsonElement</c>).</item>
    /// </list>
    /// </remarks>
    public static Value FromJson(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null => Value.Null.Instance,
            JsonValueKind.True => Value.Boolean.True,
            JsonValueKind.False => Value.Boolean.False,
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.String => new Value.String(element.GetString() ?? string.Empty),
            JsonValueKind.Array => ReadArray(element),
            JsonValueKind.Object => ReadObject(element),
            JsonValueKind.Undefined => Value.Undefined.Instance,
            _ => Value.Undefined.Instance,
        };

    /// <summary>Parses a UTF-8 encoded JSON payload into a <see cref="Value"/>.</summary>
    public static Value FromJson(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);

        return FromJson(doc.RootElement);
    }

    /// <summary>Parses a JSON string into a <see cref="Value"/>.</summary>
    public static Value FromJson(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);

        return FromJson(doc.RootElement);
    }

    /// <summary>
    /// Writes a <see cref="Value"/> to the given <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><see cref="Value.Undefined"/> at the top level writes <c>null</c>; inside an
    ///   object it causes the enclosing <see cref="WriteObject"/> to skip the key; inside an
    ///   array it writes <c>null</c> (arrays have no "omit" semantics in JSON).</item>
    ///   <item><see cref="Value.Function"/> throws <see cref="InvalidOperationException"/>
    ///   because functions are not representable in JSON.</item>
    /// </list>
    /// </remarks>
    public static void WriteValue(Utf8JsonWriter writer, Value value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        switch (value)
        {
            case Value.Null:
                writer.WriteNullValue();
                break;
            case Value.Undefined:
                // Undefined inside an array becomes null (JSON has no undefined);
                // callers writing objects use WriteObject which skips undefined values.
                writer.WriteNullValue();
                break;
            case Value.Boolean b:
                writer.WriteBooleanValue(b.V);
                break;
            case Value.Number n:
                WriteNumber(writer, n.V);
                break;
            case Value.String s:
                writer.WriteStringValue(s.V);
                break;
            case Value.Array a:
                writer.WriteStartArray();
                foreach (Value item in a.Items)
                {
                    WriteValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            case Value.Object o:
                WriteObject(writer, o.Props);
                break;
            case Value.Function:
                throw new InvalidOperationException(
                    "Cannot serialise Value.Function to JSON: functions are not JSON-representable."
                );
            default:
                throw new InvalidOperationException(
                    $"Unknown Value variant: {value.GetType().Name}"
                );
        }
    }

    /// <summary>
    /// Writes a dictionary of named values as a JSON object, skipping any
    /// entry whose value is <see cref="Value.Undefined"/> or
    /// <see cref="Value.Function"/>.
    /// </summary>
    public static void WriteObject(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, Value> properties
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(properties);
        writer.WriteStartObject();
        foreach (KeyValuePair<string, Value> kv in properties)
        {
            if (kv.Value is Value.Undefined or Value.Function)
            {
                continue;
            }
            // Defence-in-depth: never emit prototype-pollution keys to
            // downstream consumers (other JSON / JS runtimes). Access inside
            // Expreszo is already blocked by ExpressionValidator at read time.
            if (ExpressionValidator.DangerousProperties.Contains(kv.Key))
            {
                continue;
            }
            writer.WritePropertyName(kv.Key);
            WriteValue(writer, kv.Value);
        }
        writer.WriteEndObject();
    }

    /// <summary>Serialises a <see cref="Value"/> to a UTF-8 byte array.</summary>
    public static byte[] ToUtf8Bytes(Value value)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteValue(writer, value);
        }
        return buffer.ToArray();
    }

    /// <summary>Serialises a <see cref="Value"/> to a JSON string.</summary>
    public static string ToJsonString(Value value)
    {
        return System.Text.Encoding.UTF8.GetString(ToUtf8Bytes(value));
    }

    private static Value.Number ReadNumber(JsonElement element)
    {
        // Prefer double. If the source happens to be an integer too large for
        // double to represent exactly, we accept the lossy conversion to match
        // JavaScript's Number semantics (single IEEE-754 double space).
        if (element.TryGetDouble(out double d))
        {
            return Value.Number.Of(d);
        }
        // Shouldn't happen for JsonValueKind.Number but fall back defensively.
        return Value.Number.Of(0);
    }

    private static Value.Array ReadArray(JsonElement element)
    {
        ImmutableArray<Value>.Builder builder = ImmutableArray.CreateBuilder<Value>(
            element.GetArrayLength()
        );
        foreach (JsonElement item in element.EnumerateArray())
        {
            builder.Add(FromJson(item));
        }
        return new Value.Array(builder.ToImmutable());
    }

    private static Value.Object ReadObject(JsonElement element)
    {
        var tmp = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            // Defence-in-depth: drop prototype-pollution-style keys at the
            // JSON boundary so they never reach scope / member access.
            if (ExpressionValidator.DangerousProperties.Contains(property.Name))
            {
                continue;
            }
            // System.Text.Json preserves the last occurrence when you index by
            // name; we match that here explicitly so the semantics are predictable.
            tmp[property.Name] = FromJson(property.Value);
        }
        if (tmp.Count == 0)
        {
            return Value.Object.Empty;
        }
        return new Value.Object(tmp.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static void WriteNumber(Utf8JsonWriter writer, double v)
    {
        // JSON cannot represent NaN / Infinity. Match JavaScript's
        // JSON.stringify behaviour (which emits null for these).
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteNumberValue(v);
    }
}
