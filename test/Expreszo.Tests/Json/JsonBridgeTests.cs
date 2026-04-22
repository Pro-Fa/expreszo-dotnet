using System.Collections.Immutable;
using System.Text.Json;

namespace Expreszo.Tests.Json;

public class JsonBridgeTests
{
    // ----- FromJson: each JsonValueKind -----

    [Test]
    public async Task FromJson_converts_null()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("null");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        await Assert.That(v).IsSameReferenceAs(Value.Null.Instance);
    }

    [Test]
    public async Task FromJson_converts_booleans()
    {
        // Arrange
        using JsonDocument t = JsonDocument.Parse("true");
        using JsonDocument f = JsonDocument.Parse("false");

        // Act
        Value trueValue = JsonBridge.FromJson(t.RootElement);
        Value falseValue = JsonBridge.FromJson(f.RootElement);

        // Assert
        await Assert.That(trueValue).IsSameReferenceAs(Value.Boolean.True);
        await Assert.That(falseValue).IsSameReferenceAs(Value.Boolean.False);
    }

    [Test]
    [Arguments("0", 0d)]
    [Arguments("1", 1d)]
    [Arguments("-1", -1d)]
    [Arguments("3.14", 3.14d)]
    [Arguments("1e10", 1e10)]
    public async Task FromJson_converts_numbers(string json, double expected)
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse(json);

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        await Assert.That(v).IsTypeOf<Value.Number>();
        await Assert.That(((Value.Number)v).V).IsEqualTo(expected);
    }

    [Test]
    public async Task FromJson_converts_strings()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("\"hello\\nworld\"");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        await Assert.That(v).IsEqualTo((Value)new Value.String("hello\nworld"));
    }

    [Test]
    public async Task FromJson_converts_empty_string()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("\"\"");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        await Assert.That(v).IsEqualTo((Value)new Value.String(string.Empty));
    }

    [Test]
    public async Task FromJson_converts_empty_array()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("[]");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        await Assert.That(v).IsTypeOf<Value.Array>();
        await Assert.That(((Value.Array)v).Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task FromJson_converts_empty_object()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{}");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        await Assert.That(v).IsTypeOf<Value.Object>();
        await Assert.That(((Value.Object)v).Props.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FromJson_converts_nested_structures()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"a\":[1,2,{\"b\":true}],\"c\":null}");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        var obj = (Value.Object)v;
        await Assert.That(obj.Props["c"]).IsEqualTo((Value)Value.Null.Instance);
        var arr = (Value.Array)obj.Props["a"];
        await Assert.That(arr.Items.Length).IsEqualTo(3);
        await Assert.That(arr.Items[0]).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(arr.Items[1]).IsEqualTo((Value)Value.Number.Of(2));
        var innerObj = (Value.Object)arr.Items[2];
        await Assert.That(innerObj.Props["b"]).IsEqualTo((Value)Value.Boolean.True);
    }

    [Test]
    public async Task FromJson_object_with_duplicate_keys_keeps_last_value()
    {
        // Arrange
        // Matches System.Text.Json's indexer semantics.
        using JsonDocument doc = JsonDocument.Parse("{\"x\":1,\"x\":2}");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        var obj = (Value.Object)v;
        await Assert.That(obj.Props["x"]).IsEqualTo((Value)Value.Number.Of(2));
    }

    [Test]
    public async Task FromJson_large_integer_silently_degrades_to_double_matching_JS()
    {
        // Arrange
        // 9007199254740993 (2^53 + 1) is not representable as a double.
        using JsonDocument doc = JsonDocument.Parse("9007199254740993");

        // Act
        Value v = JsonBridge.FromJson(doc.RootElement);

        // Assert
        var n = (Value.Number)v;
        // double can't distinguish 2^53 and 2^53+1 - we expect the rounded value.
        await Assert.That(n.V).IsEqualTo(9007199254740992d);
    }

    [Test]
    public async Task FromJson_from_string_overload_parses_document()
    {
        // Arrange
        const string json = "{\"a\":1}";

        // Act
        Value v = JsonBridge.FromJson(json);

        // Assert
        var obj = (Value.Object)v;
        await Assert.That(obj.Props["a"]).IsEqualTo((Value)Value.Number.Of(1));
    }

    [Test]
    public async Task FromJson_from_utf8_bytes_overload_parses_document()
    {
        // Arrange
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("[1,2,3]");

        // Act
        Value v = JsonBridge.FromJson(bytes);

        // Assert
        var arr = (Value.Array)v;
        await Assert.That(arr.Items.Length).IsEqualTo(3);
    }

    // ----- WriteValue: each Value variant -----

    [Test]
    public async Task WriteValue_emits_null()
    {
        // Arrange

        // Act
        string json = JsonBridge.ToJsonString(Value.Null.Instance);

        // Assert
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task WriteValue_emits_undefined_as_null()
    {
        // Arrange

        // Act
        string json = JsonBridge.ToJsonString(Value.Undefined.Instance);

        // Assert
        // At top-level / in arrays, undefined becomes null in JSON output.
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task WriteValue_emits_booleans()
    {
        // Arrange

        // Act
        string trueJson = JsonBridge.ToJsonString(Value.Boolean.True);
        string falseJson = JsonBridge.ToJsonString(Value.Boolean.False);

        // Assert
        await Assert.That(trueJson).IsEqualTo("true");
        await Assert.That(falseJson).IsEqualTo("false");
    }

    [Test]
    [Arguments(0d, "0")]
    [Arguments(1d, "1")]
    [Arguments(-42d, "-42")]
    [Arguments(3.14d, "3.14")]
    public async Task WriteValue_emits_regular_numbers(double v, string expected)
    {
        // Arrange

        // Act
        string json = JsonBridge.ToJsonString(Value.Number.Of(v));

        // Assert
        await Assert.That(json).IsEqualTo(expected);
    }

    [Test]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    public async Task WriteValue_emits_NaN_and_Infinity_as_null(double v)
    {
        // Arrange

        // Act
        string json = JsonBridge.ToJsonString(Value.Number.Of(v));

        // Assert
        // Matches JavaScript's JSON.stringify behaviour.
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task WriteValue_escapes_strings()
    {
        // Arrange
        var v = new Value.String("line1\nline2\t\"quoted\"");

        // Act
        string json = JsonBridge.ToJsonString(v);

        // Assert
        // System.Text.Json's default encoder escapes " and control chars.
        await Assert.That(json.StartsWith('"') && json.EndsWith('"')).IsTrue();
        await Assert.That(json.Contains("\\n")).IsTrue();
        await Assert.That(json.Contains("\\t")).IsTrue();
    }

    [Test]
    public async Task WriteValue_emits_array()
    {
        // Arrange
        var v = new Value.Array(
            ImmutableArray.Create<Value>(
                Value.Number.Of(1),
                Value.Boolean.True,
                Value.Null.Instance,
                new Value.String("x")
            )
        );

        // Act
        string json = JsonBridge.ToJsonString(v);

        // Assert
        await Assert.That(json).IsEqualTo("[1,true,null,\"x\"]");
    }

    [Test]
    public async Task WriteValue_emits_undefined_inside_array_as_null()
    {
        // Arrange
        var v = new Value.Array(
            ImmutableArray.Create<Value>(
                Value.Number.Of(1),
                Value.Undefined.Instance,
                Value.Number.Of(3)
            )
        );

        // Act
        string json = JsonBridge.ToJsonString(v);

        // Assert
        await Assert.That(json).IsEqualTo("[1,null,3]");
    }

    [Test]
    public async Task WriteValue_emits_object_skipping_undefined_and_functions()
    {
        // Arrange
        var props = new Dictionary<string, Value>
        {
            ["a"] = Value.Number.Of(1),
            ["b"] = Value.Undefined.Instance,
            ["c"] = new Value.Function((_, _) => ValueTask.FromResult<Value>(Value.Null.Instance)),
            ["d"] = new Value.String("kept"),
        };
        Value.Object v = Value.Object.From(props);

        // Act
        string json = JsonBridge.ToJsonString(v);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.TryGetProperty("a", out _)).IsTrue();
        await Assert.That(doc.RootElement.TryGetProperty("b", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("c", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("d", out _)).IsTrue();
    }

    [Test]
    public async Task WriteValue_throws_on_top_level_function()
    {
        // Arrange
        var fn = new Value.Function((_, _) => ValueTask.FromResult<Value>(Value.Null.Instance));

        // Act
        Action act = () => JsonBridge.ToJsonString(fn);

        // Assert
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    // ----- Round-trip -----

    [Test]
    public async Task Round_trip_preserves_primitives_and_collections()
    {
        // Arrange
        Value.Object original = Value.Object.From(
            new Dictionary<string, Value>
            {
                ["n"] = Value.Number.Of(42),
                ["s"] = new Value.String("hi"),
                ["b"] = Value.Boolean.True,
                ["z"] = Value.Null.Instance,
                ["a"] = new Value.Array(
                    ImmutableArray.Create<Value>(Value.Number.Of(1), Value.Number.Of(2))
                ),
                ["o"] = Value.Object.From(
                    new Dictionary<string, Value> { ["x"] = Value.Number.Of(9) }
                ),
            }
        );

        // Act
        string json = JsonBridge.ToJsonString(original);
        Value back = JsonBridge.FromJson(json);

        // Assert
        await Assert.That(back).IsEqualTo((Value)original);
    }
}
