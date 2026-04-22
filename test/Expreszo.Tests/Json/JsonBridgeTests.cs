using System.Collections.Immutable;
using System.Text.Json;

namespace Expreszo.Tests.Json;

public class JsonBridgeTests
{
    // ----- FromJson: each JsonValueKind -----

    [Test]
    public async Task FromJson_converts_null()
    {
        using var doc = JsonDocument.Parse("null");
        await Assert.That(JsonBridge.FromJson(doc.RootElement)).IsSameReferenceAs(Value.Null.Instance);
    }

    [Test]
    public async Task FromJson_converts_booleans()
    {
        using var t = JsonDocument.Parse("true");
        using var f = JsonDocument.Parse("false");
        await Assert.That(JsonBridge.FromJson(t.RootElement)).IsSameReferenceAs(Value.Boolean.True);
        await Assert.That(JsonBridge.FromJson(f.RootElement)).IsSameReferenceAs(Value.Boolean.False);
    }

    [Test]
    [Arguments("0", 0d)]
    [Arguments("1", 1d)]
    [Arguments("-1", -1d)]
    [Arguments("3.14", 3.14d)]
    [Arguments("1e10", 1e10)]
    public async Task FromJson_converts_numbers(string json, double expected)
    {
        using var doc = JsonDocument.Parse(json);
        var v = JsonBridge.FromJson(doc.RootElement);
        await Assert.That(v).IsTypeOf<Value.Number>();
        await Assert.That(((Value.Number)v).V).IsEqualTo(expected);
    }

    [Test]
    public async Task FromJson_converts_strings()
    {
        using var doc = JsonDocument.Parse("\"hello\\nworld\"");
        var v = JsonBridge.FromJson(doc.RootElement);
        await Assert.That(v).IsEqualTo((Value)new Value.String("hello\nworld"));
    }

    [Test]
    public async Task FromJson_converts_empty_string()
    {
        using var doc = JsonDocument.Parse("\"\"");
        var v = JsonBridge.FromJson(doc.RootElement);
        await Assert.That(v).IsEqualTo((Value)new Value.String(string.Empty));
    }

    [Test]
    public async Task FromJson_converts_empty_array()
    {
        using var doc = JsonDocument.Parse("[]");
        var v = JsonBridge.FromJson(doc.RootElement);
        await Assert.That(v).IsTypeOf<Value.Array>();
        await Assert.That(((Value.Array)v).Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task FromJson_converts_empty_object()
    {
        using var doc = JsonDocument.Parse("{}");
        var v = JsonBridge.FromJson(doc.RootElement);
        await Assert.That(v).IsTypeOf<Value.Object>();
        await Assert.That(((Value.Object)v).Props.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FromJson_converts_nested_structures()
    {
        using var doc = JsonDocument.Parse("{\"a\":[1,2,{\"b\":true}],\"c\":null}");
        var v = JsonBridge.FromJson(doc.RootElement);
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
        // Matches System.Text.Json's indexer semantics.
        using var doc = JsonDocument.Parse("{\"x\":1,\"x\":2}");
        var v = JsonBridge.FromJson(doc.RootElement);
        var obj = (Value.Object)v;
        await Assert.That(obj.Props["x"]).IsEqualTo((Value)Value.Number.Of(2));
    }

    [Test]
    public async Task FromJson_large_integer_silently_degrades_to_double_matching_JS()
    {
        // 9007199254740993 (2^53 + 1) is not representable as a double.
        using var doc = JsonDocument.Parse("9007199254740993");
        var v = JsonBridge.FromJson(doc.RootElement);
        var n = (Value.Number)v;
        // double can't distinguish 2^53 and 2^53+1 — we expect the rounded value.
        await Assert.That(n.V).IsEqualTo(9007199254740992d);
    }

    [Test]
    public async Task FromJson_from_string_overload_parses_document()
    {
        var v = JsonBridge.FromJson("{\"a\":1}");
        var obj = (Value.Object)v;
        await Assert.That(obj.Props["a"]).IsEqualTo((Value)Value.Number.Of(1));
    }

    [Test]
    public async Task FromJson_from_utf8_bytes_overload_parses_document()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("[1,2,3]");
        var v = JsonBridge.FromJson(bytes);
        var arr = (Value.Array)v;
        await Assert.That(arr.Items.Length).IsEqualTo(3);
    }

    // ----- WriteValue: each Value variant -----

    [Test]
    public async Task WriteValue_emits_null()
    {
        await Assert.That(JsonBridge.ToJsonString(Value.Null.Instance)).IsEqualTo("null");
    }

    [Test]
    public async Task WriteValue_emits_undefined_as_null()
    {
        // At top-level / in arrays, undefined becomes null in JSON output.
        await Assert.That(JsonBridge.ToJsonString(Value.Undefined.Instance)).IsEqualTo("null");
    }

    [Test]
    public async Task WriteValue_emits_booleans()
    {
        await Assert.That(JsonBridge.ToJsonString(Value.Boolean.True)).IsEqualTo("true");
        await Assert.That(JsonBridge.ToJsonString(Value.Boolean.False)).IsEqualTo("false");
    }

    [Test]
    [Arguments(0d, "0")]
    [Arguments(1d, "1")]
    [Arguments(-42d, "-42")]
    [Arguments(3.14d, "3.14")]
    public async Task WriteValue_emits_regular_numbers(double v, string expected)
    {
        await Assert.That(JsonBridge.ToJsonString(Value.Number.Of(v))).IsEqualTo(expected);
    }

    [Test]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    public async Task WriteValue_emits_NaN_and_Infinity_as_null(double v)
    {
        // Matches JavaScript's JSON.stringify behaviour.
        await Assert.That(JsonBridge.ToJsonString(Value.Number.Of(v))).IsEqualTo("null");
    }

    [Test]
    public async Task WriteValue_escapes_strings()
    {
        var v = new Value.String("line1\nline2\t\"quoted\"");
        var json = JsonBridge.ToJsonString(v);
        // System.Text.Json's default encoder escapes " and control chars.
        await Assert.That(json.StartsWith('"') && json.EndsWith('"')).IsTrue();
        await Assert.That(json.Contains("\\n")).IsTrue();
        await Assert.That(json.Contains("\\t")).IsTrue();
    }

    [Test]
    public async Task WriteValue_emits_array()
    {
        var v = new Value.Array(ImmutableArray.Create<Value>(
            Value.Number.Of(1),
            Value.Boolean.True,
            Value.Null.Instance,
            new Value.String("x")));

        await Assert.That(JsonBridge.ToJsonString(v)).IsEqualTo("[1,true,null,\"x\"]");
    }

    [Test]
    public async Task WriteValue_emits_undefined_inside_array_as_null()
    {
        var v = new Value.Array(ImmutableArray.Create<Value>(
            Value.Number.Of(1),
            Value.Undefined.Instance,
            Value.Number.Of(3)));

        await Assert.That(JsonBridge.ToJsonString(v)).IsEqualTo("[1,null,3]");
    }

    [Test]
    public async Task WriteValue_emits_object_skipping_undefined_and_functions()
    {
        var props = new Dictionary<string, Value>
        {
            ["a"] = Value.Number.Of(1),
            ["b"] = Value.Undefined.Instance,
            ["c"] = new Value.Function((_, _) => ValueTask.FromResult<Value>(Value.Null.Instance)),
            ["d"] = new Value.String("kept"),
        };
        var v = Value.Object.From(props);
        var json = JsonBridge.ToJsonString(v);

        using var doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.TryGetProperty("a", out _)).IsTrue();
        await Assert.That(doc.RootElement.TryGetProperty("b", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("c", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("d", out _)).IsTrue();
    }

    [Test]
    public async Task WriteValue_throws_on_top_level_function()
    {
        var fn = new Value.Function((_, _) => ValueTask.FromResult<Value>(Value.Null.Instance));
        await Assert.That(() => JsonBridge.ToJsonString(fn)).Throws<InvalidOperationException>();
    }

    // ----- Round-trip -----

    [Test]
    public async Task Round_trip_preserves_primitives_and_collections()
    {
        var original = Value.Object.From(new Dictionary<string, Value>
        {
            ["n"] = Value.Number.Of(42),
            ["s"] = new Value.String("hi"),
            ["b"] = Value.Boolean.True,
            ["z"] = Value.Null.Instance,
            ["a"] = new Value.Array(ImmutableArray.Create<Value>(Value.Number.Of(1), Value.Number.Of(2))),
            ["o"] = Value.Object.From(new Dictionary<string, Value> { ["x"] = Value.Number.Of(9) }),
        });

        var json = JsonBridge.ToJsonString(original);
        var back = JsonBridge.FromJson(json);

        await Assert.That(back).IsEqualTo((Value)original);
    }
}
