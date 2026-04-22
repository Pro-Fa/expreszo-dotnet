# Expreszo .NET

A safe, extensible expression evaluator for .NET — a configurable alternative to `eval()`. Parses and evaluates the same expression language as [`expreszo-typescript`](https://github.com/Pro-Fa/expreszo-typescript).

## Highlights

- Pratt parser, immutable AST, 36 operators, 71 built-in functions.
- **JsonDocument-based I/O** — variables in, results out, using `System.Text.Json` primitives.
- Single async-capable evaluator with a synchronous fast path (`ValueTask<Value>` under the hood).
- **Native AOT and trim compatible** — zero reflection, zero runtime code generation. CI verifies on every PR.
- `net10.0` target.

## Install

```sh
dotnet add package Expreszo
```

## Quick start

```csharp
using System.Text.Json;
using Expreszo;

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
```

## Expression language — cheat sheet

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

**String** (27): `length`, `isEmpty`, `contains`, `startsWith`, `endsWith`, `searchCount`, `trim`, `toUpper`, `toLower`, `toTitle`, `split`, `repeat`, `reverse`, `left`, `right`, `replace`, `replaceFirst`, `naturalSort`, `toNumber`, `toBoolean`, `padLeft`, `padRight`, `padBoth`, `slice`, `urlEncode`, `base64Encode`, `base64Decode`, `coalesce`

**Object** (7): `merge`, `keys`, `values`, `mapValues`, `pick`, `omit`, `flattenObject`

**Utility** (2): `if` (lazy), `json`

**Type-check** (8): `isArray`, `isObject`, `isNumber`, `isString`, `isBoolean`, `isNull`, `isUndefined`, `isFunction`

## Design notes

Three behaviours to know about:

- **`Undefined` is distinct from `Null`.** Missing members and uninitialized variables return `Value.Undefined`; explicit JSON nulls return `Value.Null`. The `??` operator, the `isUndefined`/`isNull` functions, and short-circuit semantics all depend on this distinction.
- **Assignments don't propagate back to your input `JsonDocument`.** The evaluator copies the document into an internal scope on entry. Assignments mutate that scope only; the caller's document is untouched. (`JsonDocument` is immutable in System.Text.Json.)
- **Numbers are IEEE 754 `double`** — same semantics as JavaScript. Very large integers in your JSON lose precision; serialise them as strings if you need exact round-tripping. There is no `decimal` mode.

## Security

The library validates every access point before evaluating it:

- Properties named `__proto__`, `prototype`, or `constructor` are always rejected — both via dot access and via bracket-string access.
- Array indices must be finite non-negative integers.
- Only registered functions (built-ins + any you add via a future `OperatorTableBuilder` API) can be invoked. Raw CLR delegates smuggled through a custom resolver are rejected.

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

## Out of scope

- MCP server, language service (LSP).
- Legacy-mode semantics.
- Runtime disabling of specific operators.
- Customising the built-in operator set (adding custom named operators). Adding custom functions will be supported via `OperatorTableBuilder` in a future release.

## License

MIT.
