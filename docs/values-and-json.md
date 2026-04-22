# Values & JsonDocument

> **Audience:** Developers who want to understand the boundary between Expreszo's internal value model and the .NET JSON types their application passes in and gets out.

Expreszo's internal value model is a sealed C# discriminated union (`Expreszo.Value`). `System.Text.Json.JsonDocument` and `JsonElement` are only used at the I/O boundary, converted in and out through the `JsonBridge` utility class.

This page explains why the boundary exists, how the types map, and the few places where the two diverge.

## Why a dedicated `Value` type?

JsonElement alone isn't enough to represent an Expreszo result:

- **No `undefined`.** JSON has `null` but no `undefined`. The expression language uses both distinctly — `x ?? fallback` catches both but `isUndefined(null)` is `false`, and missing properties return `undefined` not `null`.
- **No functions.** An expression can return a lambda (`map` passes them around, `x => x * 2` is a value). JSON can't serialise these.
- **Lifetime.** `JsonElement` is a view into its owning `JsonDocument`. If the document is disposed, the element is invalid. Storing elements inside a cached `Expression` would defeat the cache — so a parallel in-memory representation is needed regardless.
- **Performance.** `JsonElement.GetProperty` is an O(n) scan through the raw UTF-8 bytes. Expreszo's `Value.Object` uses a `FrozenDictionary<string, Value>` for O(1) lookups.

The trade-off is one conversion pass at the boundary, and that's what `JsonBridge` is for.

## The `Value` hierarchy

```csharp
public abstract record Value
{
    public sealed record Number(double V) : Value;
    public sealed record String(string V) : Value;
    public sealed record Boolean(bool V) : Value;
    public sealed record Null : Value;                                   // singleton
    public sealed record Undefined : Value;                              // singleton
    public sealed record Array(ImmutableArray<Value> Items) : Value;
    public sealed record Object(FrozenDictionary<string, Value> Props) : Value;
    public sealed record Function(ExprFunc Invoke, string? Name) : Value;
}
```

### Singletons and interning

- `Value.Null.Instance` and `Value.Undefined.Instance` are singletons. Use them instead of `new()`.
- `Value.Boolean.True` / `Value.Boolean.False` are singletons. `Value.Boolean.Of(bool)` returns the right one.
- `Value.Number.Of(double)` is interned for small non-negative integers (0–255). Use it everywhere you'd otherwise write `new Value.Number(...)`.
- `Value.Array.Empty` and `Value.Object.Empty` are shared singletons for the common empty cases.

### Equality

`Value` records have structural equality:

- `Value.Number(1.0)` equals any other `Value.Number(1.0)`.
- `Value.Array` compares element-wise, recursively.
- `Value.Object` compares by key/value pairs (order-insensitive).
- `Value.Function` compares by reference — each lambda is a fresh instance.

NaN follows IEEE 754: two `Value.Number(NaN)` instances are **not** equal.

## Converting JsonDocument → Value

Top-level variables are loaded via `Scope.FromJsonDocument`:

```csharp
using var doc = JsonDocument.Parse("""
{
  "count": 42,
  "name": "Ada",
  "flag": true,
  "items": [1, 2, 3],
  "meta": { "active": true }
}
""");

parser.Evaluate("count + length(items)", doc);   // Value.Number(45)
```

Each top-level key becomes a root-scope binding. If the document's root is anything other than an object (array, string, number, `null`), the scope ends up empty.

### JsonValueKind mapping

| `JsonValueKind` | `Value` variant |
|:---|:---|
| `Null` | `Value.Null.Instance` |
| `True` | `Value.Boolean.True` |
| `False` | `Value.Boolean.False` |
| `Number` | `Value.Number(GetDouble())` |
| `String` | `Value.String(GetString())` |
| `Array` | `Value.Array` with each element recursively converted |
| `Object` | `Value.Object` with each property recursively converted; duplicate keys last-wins |
| `Undefined` | `Value.Undefined.Instance` (only produced by a default `JsonElement`) |

### Number precision

All numbers convert to `double`. Integers larger than `2^53` lose precision — matching JavaScript's Number semantics. If your payload has integer IDs that exceed that range, serialise them as strings on the way in.

## Converting Value → JSON

Use `JsonBridge` to write a `Value` back out:

```csharp
using Expreszo.Json;

// Serialise to a UTF-8 byte array
byte[] utf8 = JsonBridge.ToUtf8Bytes(value);

// Serialise to a string
string json = JsonBridge.ToJsonString(value);

// Write into an existing Utf8JsonWriter
var writer = new Utf8JsonWriter(stream);
JsonBridge.WriteValue(writer, value);
```

### Mapping rules

| `Value` variant | JSON output |
|:---|:---|
| `Null` | `null` |
| `Undefined` | `null` (top-level and in arrays); **omitted** from objects |
| `Boolean` | `true` / `false` |
| `Number` | The numeric value — except `NaN` / ±`Infinity`, which emit `null` (matches `JSON.stringify`) |
| `String` | Escaped JSON string |
| `Array` | Each element recursively written |
| `Object` | Each key/value pair; `Undefined` / `Function` values are skipped |
| `Function` | Throws `InvalidOperationException` at the top level |

`WriteObject(writer, dictionary)` is a separate entry point that writes an `IReadOnlyDictionary<string, Value>` directly — used internally by `Scope.ToJsonString()`.

## Assignments don't mutate the input

`JsonDocument` is immutable in `System.Text.Json`. When an expression assigns (`x = 5`), Expreszo can't (and shouldn't) write back into the caller's document. Instead:

1. On entry, `Evaluate` / `EvaluateAsync` copies the top-level keys of the input `JsonDocument` into a fresh internal `Scope`.
2. Assignments mutate the internal scope.
3. The scope is discarded when the call returns.

The caller's `JsonDocument` is never modified. This is **intentional** — it keeps evaluation side-effect-free at the API boundary and avoids the lifetime hazards of mutating a shared document.

### Reading post-assignment state

If you need to see what the scope contains after evaluation, don't rely on an `Evaluate` overload — there isn't one. Instead, call `Scope.ToJsonString()` if you construct the scope manually, or restructure your expression so the final statement returns what you want to observe.

```csharp
// Bundle the outputs into a single result object:
var r = parser.Evaluate("x = 5; y = 10; { x: x, y: y, sum: x + y }", null);
// Value.Object({ x: 5, y: 10, sum: 15 })
```

## Lifetime

- `Value` instances are ordinary managed objects — no lifetime coupling to any `JsonDocument`.
- `JsonDocument` is `IDisposable` and holds a pooled buffer. Always `using`-dispose it (or pass `Dispose` responsibility explicitly).
- If you need to keep structured results past the `JsonDocument`'s life, convert to `Value` via `JsonBridge.FromJson(...)` — that severs the lifetime dependency.

## AOT safety

All conversions are built on `JsonElement` accessors (`GetProperty`, `EnumerateArray`, `GetDouble`, etc.) and `Utf8JsonWriter` — no `JsonSerializer`, no reflection, no source generator needed. Your `<PublishAot>true</PublishAot>` build stays clean. See [AOT & Trimming](aot-and-trimming.md) for details.

## See Also

- [Parser](parser.md) — the entry points that take `JsonDocument` / `JsonElement`.
- [Expression](expression.md) — evaluate, simplify, substitute.
- [Advanced Features](advanced-features.md) — async, resolvers, `undefined` semantics.
