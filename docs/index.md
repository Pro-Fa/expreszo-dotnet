# Expreszo .NET

[![NuGet](https://img.shields.io/nuget/v/Expreszo.svg?maxAge=3600)](https://www.nuget.org/packages/Expreszo)

**A safe, extensible expression evaluator for .NET — a configurable alternative to `eval()` that won't execute arbitrary code.**

Expreszo parses and evaluates expressions at runtime. Use it to power user-facing formula editors, rule engines, template systems, or any place you need to evaluate dynamic expressions safely. It's a C# port of [`expreszo-typescript`](https://github.com/pro-fa/expreszo-typescript) and speaks the same expression language.

## Why Expreszo?

### Fast

Expreszo uses a **Pratt parser** — a top-down operator-precedence algorithm that processes tokens in a single pass with no backtracking. Parsing is predictable and linear, error positions are precise, and the parser produces an **immutable AST** that can be evaluated repeatedly against different variable sets with near-zero overhead.

A single walker handles both synchronous and asynchronous evaluation via `ValueTask<Value>`. When every registered function completes synchronously, the async state machine never allocates.

### Safe

- **No code execution** — expressions can only call functions registered on the parser.
- **Prototype pollution protection** — access to `__proto__`, `prototype`, and `constructor` is blocked in both dot and bracket notation.
- **Array index validation** — non-integer and out-of-range indices are rejected or clamped, not silently cast.
- **Recursion depth limit** — deeply nested expressions are rejected at parse time.
- **Function allow-listing** — only delegates registered through the built-in preset can be invoked, even if a raw CLR delegate is smuggled in through a scope or resolver.

### AOT-friendly

The library assembly is built with `IsAotCompatible=true` and every trim / AOT analyser enabled. No reflection, no runtime code generation, no `System.Text.Json` reflection-based serialization. CI verifies on every PR that `dotnet publish -p:PublishAot=true` of the sample app succeeds with zero warnings.

Your app can enable `<PublishAot>true</PublishAot>` without any warnings from Expreszo.

### JSON-native

Variables in and results out are `System.Text.Json` primitives — `JsonDocument` / `JsonElement` / `Utf8JsonWriter`. No reflection-based deserialization, no intermediate `Dictionary<string, object>` bags, no lifetime gotchas.

## Installation

```sh
dotnet add package Expreszo
```

Targets `net10.0`.

## Quick start

```csharp
using System.Text.Json;
using Expreszo;

var parser = new Parser();

// Simple evaluation.
var r1 = parser.Evaluate("2 * x + 1", JsonDocument.Parse("""{"x":3}"""));
Console.WriteLine(r1);  // 7

// Parse once, evaluate many times.
var expr = parser.Parse("base * qty * (1 - discount)");
using var values = JsonDocument.Parse("""{"base":25,"qty":4,"discount":0.1}""");
Console.WriteLine(expr.Evaluate(values));  // 90

// Higher-order: lambdas and array callbacks.
using var data = JsonDocument.Parse("""{"items":[10,20,30,40]}""");
Console.WriteLine(parser.Evaluate("sum(filter(items, x => x > 15))", data));  // 90
```

## Documentation

### For Expression Writers

- [Quick Reference](quick-reference.md) — Cheat sheet of operators, functions, and syntax.
- [Expression Syntax](syntax.md) — Complete syntax reference with examples.

### For Developers

- [Parser](parser.md) — Parser class: options, parsing, evaluating, custom functions.
- [Expression](expression.md) — `Expression` object methods: `Evaluate`, `EvaluateAsync`, `Simplify`, `Substitute`, `Variables`, `Symbols`, `ToString`, `Accept<T>`.
- [Advanced Features](advanced-features.md) — Async functions, custom variable resolution, `??` coalesce, optional chaining, `not in`, concatenation, type conversion, CASE, object construction, `if` lazy evaluation.
- [Values & JsonDocument](values-and-json.md) — The `Value` union, Undefined vs Null, assignment semantics, round-tripping JSON.
- [Security & Validation](security.md) — `ExpressionValidator`, dangerous-property blocklist, array index validation, function allow-listing.
- [AOT & Trimming](aot-and-trimming.md) — Native AOT guarantees, analyser settings, how to keep your app AOT-clean.

### For Contributors

- [Contributing](contributing.md) — Development setup, project layout, testing, and PR guidelines.

## License

MIT. See [LICENSE.txt](https://github.com/pro-fa/expreszo-dotnet/blob/main/LICENSE.txt).
