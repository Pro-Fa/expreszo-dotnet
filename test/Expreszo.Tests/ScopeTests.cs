using System.Text.Json;

namespace Expreszo.Tests;

public class ScopeTests
{
    [Test]
    public async Task TryGet_returns_false_on_empty_scope()
    {
        var scope = new Scope();
        await Assert.That(scope.TryGet("x", out _)).IsFalse();
    }

    [Test]
    public async Task Assign_writes_to_current_frame()
    {
        var scope = new Scope();
        scope.Assign("x", Value.Number.Of(5));

        await Assert.That(scope.TryGet("x", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo((Value)Value.Number.Of(5));
    }

    [Test]
    public async Task TryGet_walks_up_parent_chain()
    {
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        var child = parent.CreateChild();

        await Assert.That(child.TryGet("x", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo((Value)Value.Number.Of(1));
    }

    [Test]
    public async Task Assign_in_child_does_not_mutate_parent()
    {
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        var child = parent.CreateChild();

        child.Assign("x", Value.Number.Of(99));

        parent.TryGet("x", out var parentX);
        child.TryGet("x", out var childX);

        await Assert.That(parentX).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(childX).IsEqualTo((Value)Value.Number.Of(99));
    }

    [Test]
    public async Task Local_in_child_shadows_parent()
    {
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        var child = parent.CreateChild();
        child.SetLocal("x", Value.Number.Of(2));

        child.TryGet("x", out var v);
        await Assert.That(v).IsEqualTo((Value)Value.Number.Of(2));
    }

    [Test]
    public async Task Remove_only_affects_current_frame()
    {
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        var child = parent.CreateChild();
        child.SetLocal("x", Value.Number.Of(2));

        var removed = child.Remove("x");
        await Assert.That(removed).IsTrue();

        // Removing x from child unshadows the parent's x
        child.TryGet("x", out var v);
        await Assert.That(v).IsEqualTo((Value)Value.Number.Of(1));
    }

    [Test]
    public async Task FromJsonDocument_populates_root_scope_from_object()
    {
        using var doc = JsonDocument.Parse("{\"a\":1,\"b\":\"hi\",\"c\":true,\"d\":null}");

        var scope = Scope.FromJsonDocument(doc);

        scope.TryGet("a", out var a);
        scope.TryGet("b", out var b);
        scope.TryGet("c", out var c);
        scope.TryGet("d", out var d);

        await Assert.That(a).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(b).IsEqualTo((Value)new Value.String("hi"));
        await Assert.That(c).IsEqualTo((Value)Value.Boolean.True);
        await Assert.That(d).IsEqualTo((Value)Value.Null.Instance);
    }

    [Test]
    public async Task FromJsonDocument_returns_empty_for_null_document()
    {
        var scope = Scope.FromJsonDocument(null);
        await Assert.That(scope.Locals.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("[1,2,3]")]
    [Arguments("42")]
    [Arguments("\"just a string\"")]
    [Arguments("null")]
    public async Task FromJsonDocument_returns_empty_for_non_object_roots(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var scope = Scope.FromJsonDocument(doc);
        await Assert.That(scope.Locals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ToJsonString_emits_current_frame_only_and_skips_functions_and_undefined()
    {
        var parent = new Scope();
        parent.Assign("shouldNotAppear", Value.Number.Of(99));

        var child = parent.CreateChild();
        child.SetLocal("kept", Value.Number.Of(1));
        child.SetLocal("droppedUndefined", Value.Undefined.Instance);
        child.SetLocal("droppedFunction", new Value.Function((_, _) => ValueTask.FromResult<Value>(Value.Null.Instance)));

        var json = child.ToJsonString();

        // Parse back and assert contents are exactly the single "kept" key.
        using var doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(doc.RootElement.TryGetProperty("kept", out var kept)).IsTrue();
        await Assert.That(kept.GetDouble()).IsEqualTo(1d);
        await Assert.That(doc.RootElement.TryGetProperty("droppedUndefined", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("droppedFunction", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("shouldNotAppear", out _)).IsFalse();
    }

    [Test]
    public async Task Assignments_are_isolated_across_sibling_scopes()
    {
        var parent = new Scope();
        var a = parent.CreateChild();
        var b = parent.CreateChild();

        a.Assign("x", Value.Number.Of(1));
        b.Assign("x", Value.Number.Of(2));

        a.TryGet("x", out var ax);
        b.TryGet("x", out var bx);

        await Assert.That(ax).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(bx).IsEqualTo((Value)Value.Number.Of(2));
    }
}
