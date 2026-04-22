# Parser

> **Audience:** Developers integrating ExpresZo into their .NET projects.

The `Parser` class is the main entry point. Construct one, reuse it across many calls - `Parser` and the `Expression`s it produces are immutable after construction and safe for concurrent use.

## Quick Start

```csharp
using System.Text.Json;
using Expreszo;

// Create a parser instance
var parser = new Parser();

// Parse once, evaluate many times
var expr = parser.Parse("2 * x + 1");
using var a = JsonDocument.Parse("""{"x":3}""");
using var b = JsonDocument.Parse("""{"x":5}""");
expr.Evaluate(a);     // Value.Number(7)
expr.Evaluate(b);     // Value.Number(11)

// Or the one-shot overload
parser.Evaluate("2 * x + 1", a);    // Value.Number(7)
```

## Constructor

```csharp
public Parser(ParserOptions? options = null);
```

### `ParserOptions`

| Property | Type | Default | Description |
|:---------|:-----|:--------|:------------|
| `AllowMemberAccess` | `bool` | `true` | Whether dot access (`obj.prop`) is permitted. When `false`, the parser rejects any `.` access with an [`AccessException`](security.md). |

Example:

```csharp
var strict = new Parser(new ParserOptions { AllowMemberAccess = false });
strict.Evaluate("obj.prop");   // throws AccessException
```

> Operator toggling (`in` / `as` / `=` individually) is **not** supported in this port - the built-in operator set is fixed. If you need to restrict the language surface, validate the AST after parsing using `Expression.Accept<T>(INodeVisitor<T>)`.

## Methods

### `Parse(string expression)`

Converts an expression string into an [`Expression`](expression.md) object that can be evaluated many times.

```csharp
var expr = parser.Parse("x * 2 + y");
```

Throws [`ParseException`](security.md#exception-hierarchy) for syntax errors.

### `Evaluate(string expression, JsonDocument? values = null, VariableResolver? resolver = null)`

Synchronous shorthand for `Parse(expression).Evaluate(values, resolver)`.

```csharp
using var values = JsonDocument.Parse("""{"x":2,"y":3}""");
parser.Evaluate("x + y", values);     // Value.Number(5)
```

The optional `resolver` is a per-call [variable resolver](advanced-features.md#custom-variable-resolution). It runs before the parser-level resolver when a name isn't found in `values`.

```csharp
VariableResolver lookup = name =>
    name.StartsWith("$") ? new VariableResolveResult.Bound(LookupValue(name[1..])) : VariableResolveResult.NotResolved;

parser.Evaluate("$a + $b", values: null, resolver: lookup);
```

Throws [`AsyncRequiredException`](advanced-features.md#async-evaluation) if any function in the expression requires asynchronous evaluation. Use `EvaluateAsync` in that case.

### `EvaluateAsync(string expression, JsonDocument? values = null, VariableResolver? resolver = null, CancellationToken ct = default)`

Asynchronous version. Returns `ValueTask<Value>` - which **completes synchronously** for expressions that only use sync functions, so there's no overhead when async isn't needed.

```csharp
var result = await parser.EvaluateAsync("fetchData(id) * 2", values, ct: ct);
```

Honours the cancellation token at every function-call boundary.

## Value Types

All evaluation results are `Expreszo.Value`, a sealed discriminated union:

| Variant | Description |
|:--------|:------------|
| `Value.Number(double V)` | IEEE 754 double. Small non-negative integers (0–255) are interned. |
| `Value.String(string V)` | Immutable string. |
| `Value.Boolean(bool V)` | Singleton `True` / `False` via `Value.Boolean.Of(...)`. |
| `Value.Null` | Singleton `Value.Null.Instance`. |
| `Value.Undefined` | Singleton `Value.Undefined.Instance`. **Distinct from Null.** |
| `Value.Array(ImmutableArray<Value> Items)` | Structural equality. |
| `Value.Object(FrozenDictionary<string, Value> Props)` | Structural equality. |
| `Value.Function(ExprFunc Invoke, string? Name)` | Callable. Lambdas captured from expressions show up as `Value.Function` too. |

Pattern-match to branch:

```csharp
var result = parser.Evaluate("...", doc);
var display = result switch
{
    Value.Number n  => $"num {n.V}",
    Value.String s  => $"str {s.V}",
    Value.Boolean b => $"bool {b.V}",
    Value.Array a   => $"array[{a.Items.Length}]",
    Value.Object o  => $"object({o.Props.Count})",
    Value.Null      => "null",
    Value.Undefined => "undefined",
    Value.Function  => "[function]",
    _               => "?",
};
```

See [Values & JsonDocument](values-and-json.md) for details on the boundary between `Value` and `System.Text.Json`.

## Thread Safety

- **`Parser`** is immutable after construction; share instances freely.
- **`Expression`** is immutable and caches `ToString()` / symbol lists lazily with thread-safe initialisation; share instances freely.
- **Evaluations** are per-call: each `Evaluate` / `EvaluateAsync` builds a fresh `Scope` from the supplied `JsonDocument`, so a single `Expression` can be evaluated concurrently as long as each call passes its own `JsonDocument`.

## Variable Resolution Order

When an expression references a name, ExpresZo looks it up in this order:

1. **Built-in function** - e.g. `max`, `sum`, `map`.
2. **Unary operator function** - e.g. `sin`, `cos`, `sqrt`, `length`. Lets expressions pass bare `sin` as a function value.
3. **Local and parent scopes** - variables assigned with `=`, lambda parameters.
4. **Values from the input `JsonDocument`** - top-level properties become root-scope bindings.
5. **Per-call `VariableResolver`** - passed to `Evaluate` / `EvaluateAsync`.
6. **Numeric constants** - `PI`, `E`, `Infinity`, `NaN`.

If none of these resolve the name, a [`VariableException`](security.md#exception-hierarchy) is thrown.

See [Advanced Features - Custom Variable Resolution](advanced-features.md#custom-variable-resolution) for resolver patterns.

## Built-in Constants

These names are always resolvable:

| Name | Value |
|:-----|:------|
| `PI` | `Math.PI` |
| `E`  | `Math.E` |
| `Infinity` | `double.PositiveInfinity` |
| `NaN` | `double.NaN` |
| `true` / `false` | boolean literals |
| `null` | `Value.Null.Instance` |
| `undefined` | `Value.Undefined.Instance` (keyword, not constant) |

## See Also

- [Expression](expression.md) - `Evaluate`, `Simplify`, `Substitute`, `Variables`, `Symbols`, `ToString`, `Accept`.
- [Expression Syntax](syntax.md) - language reference.
- [Advanced Features](advanced-features.md) - async, resolvers, coalesce, CASE, object literals.
- [Values & JsonDocument](values-and-json.md) - I/O boundary details.
