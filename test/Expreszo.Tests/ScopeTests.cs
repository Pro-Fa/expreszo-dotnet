using System.Text.Json;

namespace Expreszo.Tests;

public class ScopeTests
{
    [Test]
    public async Task TryGet_returns_false_on_empty_scope()
    {
        // Arrange
        var scope = new Scope();

        // Act
        bool found = scope.TryGet("x", out _);

        // Assert
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task Assign_writes_to_current_frame()
    {
        // Arrange
        var scope = new Scope();

        // Act
        scope.Assign("x", Value.Number.Of(5));

        // Assert
        await Assert.That(scope.TryGet("x", out Value? value)).IsTrue();
        await Assert.That(value).IsEqualTo((Value)Value.Number.Of(5));
    }

    [Test]
    public async Task TryGet_walks_up_parent_chain()
    {
        // Arrange
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        Scope child = parent.CreateChild();

        // Act
        bool found = child.TryGet("x", out Value? value);

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo((Value)Value.Number.Of(1));
    }

    [Test]
    public async Task Assign_in_child_does_not_mutate_parent()
    {
        // Arrange
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        Scope child = parent.CreateChild();

        // Act
        child.Assign("x", Value.Number.Of(99));

        // Assert
        parent.TryGet("x", out Value? parentX);
        child.TryGet("x", out Value? childX);
        await Assert.That(parentX).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(childX).IsEqualTo((Value)Value.Number.Of(99));
    }

    [Test]
    public async Task Local_in_child_shadows_parent()
    {
        // Arrange
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        Scope child = parent.CreateChild();

        // Act
        child.SetLocal("x", Value.Number.Of(2));

        // Assert
        child.TryGet("x", out Value? v);
        await Assert.That(v).IsEqualTo((Value)Value.Number.Of(2));
    }

    [Test]
    public async Task Remove_only_affects_current_frame()
    {
        // Arrange
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));
        Scope child = parent.CreateChild();
        child.SetLocal("x", Value.Number.Of(2));

        // Act
        bool removed = child.Remove("x");

        // Assert
        await Assert.That(removed).IsTrue();
        // Removing x from child unshadows the parent's x
        child.TryGet("x", out Value? v);
        await Assert.That(v).IsEqualTo((Value)Value.Number.Of(1));
    }

    [Test]
    public async Task FromJsonDocument_populates_root_scope_from_object()
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse("{\"a\":1,\"b\":\"hi\",\"c\":true,\"d\":null}");

        // Act
        Scope scope = Scope.FromJsonDocument(doc);

        // Assert
        scope.TryGet("a", out Value? a);
        scope.TryGet("b", out Value? b);
        scope.TryGet("c", out Value? c);
        scope.TryGet("d", out Value? d);
        await Assert.That(a).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(b).IsEqualTo((Value)new Value.String("hi"));
        await Assert.That(c).IsEqualTo((Value)Value.Boolean.True);
        await Assert.That(d).IsEqualTo((Value)Value.Null.Instance);
    }

    [Test]
    public async Task FromJsonDocument_returns_empty_for_null_document()
    {
        // Arrange

        // Act
        Scope scope = Scope.FromJsonDocument(null);

        // Assert
        await Assert.That(scope.Locals.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("[1,2,3]")]
    [Arguments("42")]
    [Arguments("\"just a string\"")]
    [Arguments("null")]
    public async Task FromJsonDocument_returns_empty_for_non_object_roots(string json)
    {
        // Arrange
        using JsonDocument doc = JsonDocument.Parse(json);

        // Act
        Scope scope = Scope.FromJsonDocument(doc);

        // Assert
        await Assert.That(scope.Locals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ToJsonString_emits_current_frame_only_and_skips_functions_and_undefined()
    {
        // Arrange
        var parent = new Scope();
        parent.Assign("shouldNotAppear", Value.Number.Of(99));
        Scope child = parent.CreateChild();
        child.SetLocal("kept", Value.Number.Of(1));
        child.SetLocal("droppedUndefined", Value.Undefined.Instance);
        child.SetLocal(
            "droppedFunction",
            new Value.Function((_, _) => ValueTask.FromResult<Value>(Value.Null.Instance))
        );

        // Act
        string json = child.ToJsonString();

        // Assert
        // Parse back and assert contents are exactly the single "kept" key.
        using JsonDocument doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(doc.RootElement.TryGetProperty("kept", out JsonElement kept)).IsTrue();
        await Assert.That(kept.GetDouble()).IsEqualTo(1d);
        await Assert.That(doc.RootElement.TryGetProperty("droppedUndefined", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("droppedFunction", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("shouldNotAppear", out _)).IsFalse();
    }

    [Test]
    public async Task Assignments_are_isolated_across_sibling_scopes()
    {
        // Arrange
        var parent = new Scope();
        Scope a = parent.CreateChild();
        Scope b = parent.CreateChild();

        // Act
        a.Assign("x", Value.Number.Of(1));
        b.Assign("x", Value.Number.Of(2));

        // Assert
        a.TryGet("x", out Value? ax);
        b.TryGet("x", out Value? bx);
        await Assert.That(ax).IsEqualTo((Value)Value.Number.Of(1));
        await Assert.That(bx).IsEqualTo((Value)Value.Number.Of(2));
    }
}
