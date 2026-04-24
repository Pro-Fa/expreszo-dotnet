<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/logo_dark.png">
  <img src="docs/logo.png" alt="ExpresZo" width="420">
</picture>

# ExpresZo .NET

[![NuGet](https://img.shields.io/nuget/v/Expreszo.svg?maxAge=3600)](https://www.nuget.org/packages/Expreszo)
[![CI](https://github.com/Pro-Fa/expreszo-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/Pro-Fa/expreszo-dotnet/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Pro-Fa/expreszo-dotnet/branch/main/graph/badge.svg)](https://codecov.io/gh/Pro-Fa/expreszo-dotnet)

A safe, extensible expression evaluator for .NET - a configurable alternative to `eval()`. Parses and evaluates the same expression language as [`expreszo-typescript`](https://github.com/Pro-Fa/expreszo-typescript).

[Read full documentation](https://pro-fa.github.io/expreszo-dotnet/)

## Highlights

- Pratt parser, immutable AST, 27 operators, 82 built-in functions.
- **JsonDocument-based I/O** - variables in, results out, using `System.Text.Json` primitives.
- Single async-capable evaluator with a synchronous fast path (`ValueTask<Value>` under the hood).
- **Native AOT and trim compatible** - zero reflection, zero runtime code generation. CI verifies on every PR.
- `net10.0` target.

## Install

```sh
dotnet add package Expreszo
```

Targets `net10.0`. Packages are published to [NuGet.org](https://www.nuget.org/packages/Expreszo) automatically on every `v*.*.*` tag - see the [releases page](https://github.com/pro-fa/expreszo-dotnet/releases) for changelogs.

## Quick start

```csharp
using System.Text.Json;
using Expreszo;
using Expreszo.Json;

var parser = new Parser();

// Simple evaluation.
var result = parser.Evaluate("1 + 2 * 3");
Console.WriteLine(result);                           // 7

// Variables from a JsonDocument.
using var scope = JsonDocument.Parse("""{"x":10,"y":32}""");
Console.WriteLine(parser.Evaluate("x + y", scope));  // 42

// Parse once, evaluate many times.
var expr = parser.Parse("base * qty * (1 - discount)");
using var v = JsonDocument.Parse("""{"base":25,"qty":4,"discount":0.1}""");
Console.WriteLine(expr.Evaluate(v));                 // 90

// Higher-order: lambdas and array callbacks.
using var data = JsonDocument.Parse("""{"items":[10,20,30,40]}""");
Console.WriteLine(parser.Evaluate("sum(filter(items, x => x > 15))", data));  // 90

// JSON in → JSON out. The result is a Value, which JsonBridge serialises
// back to System.Text.Json primitives with no reflection.
using var order = JsonDocument.Parse("""{"price":25,"qty":4,"tax":0.21}""");
var totals = parser.Evaluate(
    "{ subtotal: price * qty, total: price * qty * (1 + tax) }",
    order);
Console.WriteLine(JsonBridge.ToJsonString(totals));
// {"subtotal":100,"total":121}

// Dynamic variable resolver - called per reference, only for names the
// scope didn't already bind. Return NotResolved to fall through.
var env = new Dictionary<string, string>
{
    ["REGION"] = "eu-west-1",
    ["USER"] = "alice",
};
VariableResolver resolveFromEnv = name => env.TryGetValue(name, out var value)
    ? new VariableResolveResult.Bound(new Value.String(value))
    : VariableResolveResult.NotResolved;
Console.WriteLine(parser.Evaluate(
    "USER | \" @ \" | REGION",
    resolver: resolveFromEnv));
// alice @ eu-west-1
```

## Expression language - cheat sheet

| Category | Examples |
|---|---|
| Literals | `42`, `3.14`, `1e10`, `0xFF`, `0b1010`, `"hello"`, `true`, `false`, `null`, `undefined` |
| Arithmetic | `+`, `-`, `*`, `/`, `%`, `^` (power), `\|` (concat) |
| Comparison | `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `not in` |
| Logical | `and` / `&&`, `or` / `\|\|`, `not` / `!` |
| Ternary / coalesce | `cond ? a : b`, `x ?? fallback` |
| Type cast | `x as "number"`, `x as "int"`, `x as "boolean"` |
| Member access | `obj.name`, `xs[0]` |
| Arrays & spread | `[1, 2, ...rest]` |
| Objects & spread | `{ a: 1, "b-key": 2, ...base }` |
| Arrow functions | `x => x * 2`, `(a, b) => a + b` |
| Function defs | `f(x, y) = x + y; f(1, 2)` |
| Assignment | `x = 5; x + 1` |
| Case | `case x when 1 then "one" when 2 then "two" else "other" end` |
| Sequences | `a = 1; b = 2; a + b` |

## Built-in functions

**Math** (17): `atan2`, `clamp`, `fac`, `gamma`, `hypot`, `max`, `min`, `pow`, `random`, `roundTo`, `sum`, `mean`, `median`, `mostFrequent`, `variance`, `stddev`, `percentile`

**Array** (20): `count`, `filter`, `fold`, `reduce`, `find`, `some`, `every`, `unique`, `distinct`, `indexOf`, `join`, `map`, `range`, `chunk`, `union`, `intersect`, `groupBy`, `countBy`, `sort`, `flatten`

**String** (28): `length`, `isEmpty`, `contains`, `startsWith`, `endsWith`, `searchCount`, `trim`, `toUpper`, `toLower`, `toTitle`, `split`, `repeat`, `reverse`, `left`, `right`, `replace`, `replaceFirst`, `naturalSort`, `toNumber`, `toBoolean`, `padLeft`, `padRight`, `padBoth`, `slice`, `urlEncode`, `base64Encode`, `base64Decode`, `coalesce`

**Object** (7): `merge`, `keys`, `values`, `mapValues`, `pick`, `omit`, `flattenObject`

**Utility** (2): `if` (lazy), `json`

**Type-check** (8): `isArray`, `isObject`, `isNumber`, `isString`, `isBoolean`, `isNull`, `isUndefined`, `isFunction`

## Design notes

Three behaviours to know about:

- **`Undefined` is distinct from `Null`.** Missing members (`obj.nope`) and lambda parameters you didn't pass return `Value.Undefined`; explicit JSON nulls return `Value.Null`. Reading a completely unknown identifier is a hard error - it throws `VariableException` rather than returning `Undefined`, so typos don't silently evaluate to nothing. The `??` operator, `isUndefined` / `isNull`, and short-circuit semantics all depend on the Null/Undefined distinction.
- **Assignments don't propagate back to your input `JsonDocument`.** The evaluator copies the document into an internal scope on entry. Assignments mutate that scope only; the caller's document is untouched. (`JsonDocument` is immutable in System.Text.Json.)
- **Numbers are IEEE 754 `double`** - same semantics as JavaScript. Very large integers in your JSON lose precision; serialise them as strings if you need exact round-tripping. There is no `decimal` mode.

## Security

The library validates every access point before evaluating it:

- Properties named `__proto__`, `prototype`, or `constructor` are always rejected - both via dot access and via bracket-string access. The same names are filtered out of JSON I/O and scope bindings for defence-in-depth.
- Array indices must be finite non-negative integers.
- Only registered functions (built-ins + any you add via a future `OperatorTableBuilder` API) can be invoked. Raw CLR delegates smuggled in through a custom `VariableResolver` or `Scope` binding are rejected at the call site via an identity-based allow-list.
- Recursion is capped at 256 levels of nested calls and 256 levels of parse depth - runaway recursion (`f(x) = f(x); f(1)`) throws an `EvaluationException` instead of a `StackOverflowException`.
- Resource-heavy built-ins (`repeat`, `padLeft`/`Right`/`Both`, `range`, `fac`, postfix `!`) enforce output-size and input-range budgets published as `Expreszo.EvaluationLimits` constants.
- `EvaluateAsync`'s `CancellationToken` is observed on every call boundary and inside every looping built-in, so timeouts are honoured in bounded time.

## AOT-safe

The library assembly is built with `IsAotCompatible=true` and every trim / AOT analyser enabled. The CI matrix includes a dedicated job that runs:

```
dotnet publish samples/AotCheck --configuration Release -r linux-x64 --self-contained -p:PublishAot=true
```

and executes the resulting native binary. Any code path introducing reflection or dynamic code generation fails the build.

Your app can enable `<PublishAot>true</PublishAot>` without any warnings from this library.

## Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/)-based micro-benchmarks live in `bench/Expreszo.Benchmarks/` and cover parsing, evaluation, simplification, and end-to-end parse+evaluate cycles:

```sh
dotnet run --project bench/Expreszo.Benchmarks -c Release -- --list flat        # list available benchmarks
dotnet run --project bench/Expreszo.Benchmarks -c Release                       # run the full matrix (~minutes)
dotnet run --project bench/Expreszo.Benchmarks -c Release -- --filter '*Eval*'  # run a subset
```

## Tooling

A Language Server Protocol (LSP) implementation lives in `src/Expreszo.LanguageServer` and `src/Expreszo.LanguageServer.Host`. The host is a stdio server consumable from any LSP-aware editor (VS Code, Neovim, Zed, JetBrains, Emacs). Current coverage: diagnostics (statement-level error recovery, so one broken statement doesn't silence diagnostics elsewhere), hover, completion, signature help, document symbols, goto-definition, find-references, rename, and semantic tokens. A VS Code extension and published host binaries are the next things on the roadmap.

Error-recovering parsing is exposed on the library API too — `Parser.TryParse(string)` returns a `ParseResult` with a best-effort expression plus an `ImmutableArray<ExpressionException>`, so callers that want diagnostics without try/catch plumbing can use it directly.

## Out of scope

- MCP server.
- Legacy-mode semantics.
- Runtime disabling of specific operators.
- Customising the built-in operator set (adding custom named operators). Adding custom functions will be supported via `OperatorTableBuilder` in a future release.

## License

MIT.
