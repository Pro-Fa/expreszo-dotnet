using System.Collections.Immutable;

namespace Expreszo.Tests;

public class ValueTests
{
    // ----- singletons -----

    [Test]
    public async Task Null_instance_is_singleton_by_reference()
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Null.Instance).IsSameReferenceAs(Value.Null.Instance);
    }

    [Test]
    public async Task Undefined_instance_is_singleton_by_reference()
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Undefined.Instance).IsSameReferenceAs(Value.Undefined.Instance);
    }

    [Test]
    public async Task Null_and_Undefined_are_not_equal()
    {
        // Arrange

        // Act & Assert
        await Assert.That((Value)Value.Null.Instance).IsNotEqualTo(Value.Undefined.Instance);
    }

    [Test]
    public async Task Boolean_true_and_false_are_singletons()
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Boolean.Of(true)).IsSameReferenceAs(Value.Boolean.True);
        await Assert.That(Value.Boolean.Of(false)).IsSameReferenceAs(Value.Boolean.False);
    }

    // ----- Number caching -----

    [Test]
    [Arguments(0d)]
    [Arguments(1d)]
    [Arguments(42d)]
    [Arguments(255d)]
    public async Task Number_Of_caches_small_non_negative_integers(double v)
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Number.Of(v)).IsSameReferenceAs(Value.Number.Of(v));
    }

    [Test]
    [Arguments(256d)]
    [Arguments(-1d)]
    [Arguments(0.5d)]
    [Arguments(1e9)]
    public async Task Number_Of_does_not_cache_outside_range_or_non_integers(double v)
    {
        // Arrange

        // Act
        Value.Number a = Value.Number.Of(v);
        Value.Number b = Value.Number.Of(v);

        // Assert
        await Assert.That(a).IsNotSameReferenceAs(b);
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Number_Of_does_not_cache_NaN()
    {
        // Arrange

        // Act
        Value.Number a = Value.Number.Of(double.NaN);
        Value.Number b = Value.Number.Of(double.NaN);

        // Assert
        await Assert.That(a).IsNotSameReferenceAs(b);
        // NaN != NaN even in record equality (double.Equals defers to object semantics).
        await Assert.That(double.IsNaN(a.V)).IsTrue();
    }

    [Test]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    public async Task Number_Of_does_not_cache_infinity(double v)
    {
        // Arrange

        // Act
        Value.Number a = Value.Number.Of(v);
        Value.Number b = Value.Number.Of(v);

        // Assert
        await Assert.That(a).IsNotSameReferenceAs(b);
        await Assert.That(a).IsEqualTo(b);
    }

    // ----- IsTruthy (JS parity) -----

    [Test]
    public async Task Falsy_values_return_false_from_IsTruthy()
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Null.Instance.IsTruthy()).IsFalse();
        await Assert.That(Value.Undefined.Instance.IsTruthy()).IsFalse();
        await Assert.That(Value.Boolean.False.IsTruthy()).IsFalse();
        await Assert.That(Value.Number.Of(0).IsTruthy()).IsFalse();
        await Assert.That(new Value.Number(double.NaN).IsTruthy()).IsFalse();
        await Assert.That(Value.String.Empty.IsTruthy()).IsFalse();
    }

    [Test]
    public async Task Truthy_values_return_true_from_IsTruthy()
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Boolean.True.IsTruthy()).IsTrue();
        await Assert.That(Value.Number.Of(1).IsTruthy()).IsTrue();
        await Assert.That(Value.Number.Of(-1).IsTruthy()).IsTrue();
        await Assert.That(new Value.String("hello").IsTruthy()).IsTrue();
        // matches JS: empty arrays and objects are truthy
        await Assert.That(Value.Array.Empty.IsTruthy()).IsTrue();
        await Assert.That(Value.Object.Empty.IsTruthy()).IsTrue();
    }

    [Test]
    public async Task String_zero_and_space_are_truthy_per_JS_semantics()
    {
        // Arrange

        // Act & Assert
        await Assert.That(new Value.String("0").IsTruthy()).IsTrue();
        await Assert.That(new Value.String(" ").IsTruthy()).IsTrue();
    }

    // ----- TypeName -----

    [Test]
    public async Task TypeName_returns_canonical_labels()
    {
        // Arrange

        // Act & Assert
        await Assert.That(Value.Number.Of(1).TypeName()).IsEqualTo("number");
        await Assert.That(new Value.String("x").TypeName()).IsEqualTo("string");
        await Assert.That(Value.Boolean.True.TypeName()).IsEqualTo("boolean");
        await Assert.That(Value.Null.Instance.TypeName()).IsEqualTo("null");
        await Assert.That(Value.Undefined.Instance.TypeName()).IsEqualTo("undefined");
        await Assert.That(Value.Array.Empty.TypeName()).IsEqualTo("array");
        await Assert.That(Value.Object.Empty.TypeName()).IsEqualTo("object");
    }

    // ----- Array structural equality -----

    [Test]
    public async Task Array_equality_is_structural_and_order_sensitive()
    {
        // Arrange
        var a = new Value.Array(
            ImmutableArray.Create<Value>(Value.Number.Of(1), Value.Number.Of(2))
        );
        var b = new Value.Array(
            ImmutableArray.Create<Value>(Value.Number.Of(1), Value.Number.Of(2))
        );
        var c = new Value.Array(
            ImmutableArray.Create<Value>(Value.Number.Of(2), Value.Number.Of(1))
        );

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a).IsNotEqualTo(c);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Array_equality_handles_nested_arrays()
    {
        // Arrange
        Value inner1 = new Value.Array(ImmutableArray.Create<Value>(Value.Number.Of(1)));
        Value inner2 = new Value.Array(ImmutableArray.Create<Value>(Value.Number.Of(1)));
        var outer1 = new Value.Array(ImmutableArray.Create(inner1));
        var outer2 = new Value.Array(ImmutableArray.Create(inner2));

        // Act

        // Assert
        await Assert.That(outer1).IsEqualTo(outer2);
    }

    // ----- Object structural equality -----

    [Test]
    public async Task Object_equality_is_order_independent_but_value_sensitive()
    {
        // Arrange
        Value.Object a = Value.Object.From(
            new Dictionary<string, Value> { ["x"] = Value.Number.Of(1), ["y"] = Value.Number.Of(2) }
        );
        Value.Object b = Value.Object.From(
            new Dictionary<string, Value> { ["y"] = Value.Number.Of(2), ["x"] = Value.Number.Of(1) }
        );
        Value.Object c = Value.Object.From(
            new Dictionary<string, Value> { ["x"] = Value.Number.Of(1), ["y"] = Value.Number.Of(3) }
        );

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a).IsNotEqualTo(c);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }
}
