# Advanced Features

> **Audience:** Developers integrating ExpresZo who need advanced customisation and features.

This page covers integration features beyond basic parsing and evaluation. For expression syntax, see [Expression Syntax](syntax.md). For the `Parser` class itself, see [Parser](parser.md).

## Async Evaluation

Custom functions can return a non-completed `ValueTask<Value>`. When they do, the entire evaluation becomes asynchronous - call `EvaluateAsync` and await the result:

```csharp
using System.Text.Json;
using Expreszo;

var parser = new Parser();

// TODO: in a future release, user functions can be registered via
// OperatorTableBuilder.AddFunction. For now, async-returning resolvers
// illustrate the same pattern:
VariableResolver resolver = name => name switch
{
    "latency" => new VariableResolveResult.Bound(Value.Number.Of(42)),
    _ => VariableResolveResult.NotResolved,
};

var expr = parser.Parse("latency * 2");
var result = await expr.EvaluateAsync(values: null, resolver: resolver);
```

### The sync fast path

`Evaluate` and `EvaluateAsync` share a single walker returning `ValueTask<Value>`. When every step completes synchronously:

- `Evaluate` inspects `task.IsCompletedSuccessfully`, reads the result, and returns it - no async state machine allocation.
- `EvaluateAsync`'s returned `ValueTask<Value>` is already completed by the time the caller sees it, so `await` is a no-op.

When something along the way returns a non-completed `ValueTask`, `Evaluate` throws `AsyncRequiredException` and the caller should switch to `EvaluateAsync`.

### Cancellation

Pass a `CancellationToken` to `EvaluateAsync`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    var result = await expr.EvaluateAsync(doc, ct: cts.Token);
}
catch (OperationCanceledException)
{
    // timed out
}
```

The evaluator checks the token at every `Call` boundary - user-registered async functions that do their own I/O should honour the token themselves too.

## Custom Variable Resolution

`VariableResolver` is called when a name isn't found in the provided `JsonDocument`. It enables:

- Variable aliasing
- Dynamic lookup (e.g. from a database, external API)
- Custom naming conventions (`$variable` syntax, prefixed names)
- Per-request / per-tenant data sources

```csharp
// Example 1: alias resolution
VariableResolver aliasResolver = name => name switch
{
    "$v" => new VariableResolveResult.Alias("variables"),
    _    => VariableResolveResult.NotResolved,
};

using var doc = JsonDocument.Parse("""{"variables":{"a":5,"b":10}}""");
parser.Evaluate("$v.a + $v.b", doc, aliasResolver);   // Value.Number(15)


// Example 2: direct value resolution
var lookup = new Dictionary<string, Value>
{
    ["a"] = Value.Number.Of(5),
    ["b"] = Value.Number.Of(10),
};
VariableResolver valueResolver = name =>
    name.StartsWith("$") && lookup.TryGetValue(name[1..], out var v)
        ? new VariableResolveResult.Bound(v)
        : VariableResolveResult.NotResolved;

parser.Evaluate("$a + $b", values: null, resolver: valueResolver);   // Value.Number(15)
```

**Return shapes:**

- `VariableResolveResult.Bound(Value)` - return the value directly.
- `VariableResolveResult.Alias(string)` - redirect to another variable; the resolver is re-invoked for the new name.
- `VariableResolveResult.NotResolved` - fall through to the next resolution layer.

### Per-Expression Resolvers

Because the resolver is a method argument (not parser state), a single parsed `Expression` can be evaluated multiple times against different data sources without re-parsing or mutation:

```csharp
var expr = parser.Parse("$user.name + \" is \" + $user.age");

VariableResolver aliceResolver = name => name == "$user"
    ? new VariableResolveResult.Bound(ObjectOf(("name", "Alice"), ("age", 30)))
    : VariableResolveResult.NotResolved;

VariableResolver bobResolver = name => name == "$user"
    ? new VariableResolveResult.Bound(ObjectOf(("name", "Bob"), ("age", 25)))
    : VariableResolveResult.NotResolved;

expr.Evaluate(null, aliceResolver);   // "Alice is 30"
expr.Evaluate(null, bobResolver);     // "Bob is 25"
```

### Resolution Order

The evaluator consults resolvers in this order:

1. **Built-in functions and operators** - e.g. `max`, `sin`.
2. **Local and parent scopes** - values set with `=`, lambda parameters.
3. **`JsonDocument` variables** - top-level keys of the document passed to `Evaluate`.
4. **Per-call resolver** - the `resolver` argument.
5. **Numeric constants** - `PI`, `E`, `Infinity`, `NaN`.

A `VariableException` is thrown if none of these resolve the name.

## Type Conversion (`as` operator)

The `as` operator provides basic type conversion:

```csharp
parser.Evaluate("\"1.6\" as \"number\"");   // Value.Number(1.6)
parser.Evaluate("\"1.6\" as \"int\"");      // Value.Number(2)    - rounded
parser.Evaluate("\"1.6\" as \"integer\"");  // Value.Number(2)    - synonym
parser.Evaluate("\"1\" as \"boolean\"");    // Value.Boolean.True
parser.Evaluate("0 as \"boolean\"");        // Value.Boolean.False
```

Supported targets: `"number"`, `"int"` / `"integer"`, `"boolean"`.

> Custom target types (e.g. `"date"`, `"currency"`) are out of scope for this port. If you need them, convert at the .NET boundary after evaluation.

## Undefined vs Null

`undefined` and `null` are distinct values in ExpresZo - this matters for JavaScript-style `??` behaviour and for distinguishing "missing" from "explicit null".

```
x > 3 ? undefined : x
x == undefined ? 1 : 2
```

- Most operators propagate `undefined`: `2 + undefined` → `undefined`, `undefined < 3` → `undefined`.
- `?? `treats `undefined`, `null`, `Infinity`, and `NaN` as nullish; everything else passes through.
- `isNull(undefined)` → `false`, `isUndefined(null)` → `false` - check for whichever one you actually mean.

JSON has no `undefined`, so ExpresZo's JSON round-trip drops `undefined` from object outputs and emits `null` for `undefined` inside arrays. See [Values & JsonDocument](values-and-json.md) for the full story.

## Coalesce Operator (`??`)

Returns the right operand when the left is `undefined`, `null`, `Infinity`, or `NaN`:

```
x ?? 0                      // 0 if x is null/undefined
10 / 0 ?? -1                // would throw before `??` - see the note below
sqrt(-1) ?? 0               // 0 (sqrt of negative is NaN)
user.nickname ?? user.name ?? "Anonymous"
```

> Unlike JavaScript, ExpresZo throws on `x / 0` rather than returning `Infinity`. `??` still catches `NaN` (e.g. `sqrt(-1)`) and explicit `Infinity` values.

## Optional Chaining for Property Access

Property access automatically handles missing properties without throwing:

```csharp
using var doc = JsonDocument.Parse("""{"user":{"profile":{"name":"Ada"}}}""");
parser.Evaluate("user.profile.name", doc);            // "Ada"
parser.Evaluate("user.profile.email", doc);           // undefined (not an error)
parser.Evaluate("user.settings.theme", doc);          // undefined
parser.Evaluate("user.settings.theme ?? \"dark\"", doc); // "dark"
```

The same applies to bracket access: `items[99]` on a three-element array is `undefined`, not an exception.

## Not In Operator

`not in` is the logical complement of `in`:

```
"d" not in ["a", "b", "c"]   // true
"a" not in ["a", "b", "c"]   // false
```

Equivalent to `not ("a" in ["a", "b", "c"])`.

## String Concatenation

Use the `|` (pipe) operator to concatenate strings or arrays:

```
"hello" | " " | "world"     // "hello world"
"Count: " | 42              // "Count: 42" - coerces either side to string
[1, 2] | [3, 4]             // [1, 2, 3, 4]
```

The `+` operator works only on numbers. Passing strings to `+` throws an `EvaluationException` - use `|`.

## CASE Expressions

SQL-style CASE expressions provide multi-way conditionals.

**Switch-style** (comparing a value):

```
case status
  when "active"   then "✓ Active"
  when "pending"  then "⏳ Pending"
  when "inactive" then "✗ Inactive"
  else "Unknown"
end
```

Comparison uses strict equality.

**If/else-style** (condition-based):

```
case
  when score >= 90 then "A"
  when score >= 80 then "B"
  when score >= 70 then "C"
  when score >= 60 then "D"
  else "F"
end
```

The first truthy condition wins. Missing `else` with no match returns `undefined`.

## Object Construction

Build objects directly in expressions, including spreads:

```
{
  name: firstName | " " | lastName,
  age: currentYear - birthYear,
  scores: [test1, test2, test3],
  meta: {
    created: now,
    version: 1
  }
}

{ ...defaults, color: "blue" }
```

Keys can be bare identifiers or quoted strings. Quoted form lets you use characters not allowed in identifiers:

```
{ "my-key": 1, "weird.key": 2 }
```

## Array Spread

The spread operator `...` inlines an array into another array literal:

```
[1, ...rest, 5]
```

If `rest` is `[2, 3, 4]`, the result is `[1, 2, 3, 4, 5]`. Spread works only in array literal positions; it's an error elsewhere.

## `if()` Lazy Evaluation

The built-in `if` function is special-cased by the evaluator for lazy evaluation - only the selected branch is evaluated. This matches the ternary operator's short-circuit:

```
if(cond, a, b)              // only `a` evaluates when cond is truthy
```

Compare to a user-defined `myIf(c, a, b) = c ? a : b`, which eagerly evaluates all three arguments before calling the lambda.

## `json()` Function

Convert any value to a JSON string:

```
json([1, 2, 3])           // "[1,2,3]"
json({a: 1, b: 2})        // "{\"a\":1,\"b\":2}"
json("hello")             // "\"hello\""
```

Function values and `undefined` follow the same rules as [JsonBridge](values-and-json.md#converting-value-json) serialisation.

## See Also

- [Parser](parser.md) - constructor, methods, thread safety.
- [Expression](expression.md) - `Evaluate`, `Simplify`, `Substitute`, `Variables`, `Symbols`.
- [Expression Syntax](syntax.md) - complete language reference.
- [Values & JsonDocument](values-and-json.md) - the I/O boundary.
- [Security & Validation](security.md) - guardrails and the validator API.
