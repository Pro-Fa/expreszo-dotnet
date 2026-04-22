# Expreszo .NET

A safe, extensible expression evaluator for .NET — a configurable alternative to `eval()`. This library is a C# port of [`expreszo-typescript`](https://github.com/Pro-Fa/expreszo-typescript) and speaks the same expression language.

**Status:** Under active development. See [the port plan](#porting-roadmap).

## Highlights

- Same expression syntax as `expreszo-typescript`: Pratt parser, immutable AST, 36 operators, 71 built-in functions.
- **JsonDocument-based I/O** — variables in, results out, using `System.Text.Json` primitives.
- Single async-capable evaluator with synchronous fast path (`ValueTask<Value>` under the hood).
- **Native AOT and trim compatible** — zero reflection, zero runtime code generation.
- `net10.0` target.

## Design notes

Three behaviors to know about before you reach for this library:

- **`Undefined` is distinct from `Null`.** Missing members and uninitialized variables return `Value.Undefined`; explicit JSON nulls return `Value.Null`. The `??` operator, the `isUndefined` / `isNull` functions, and short-circuit semantics all depend on this distinction.
- **Assignments (`x = 5`) don't propagate back to your input `JsonDocument`.** The evaluator copies the document into an internal scope on entry. Assignments mutate that scope only; the caller's document is untouched. `Scope.ToJson()` is available if you need to read the post-evaluation scope back out.
- **Numbers are IEEE 754 `double`** — same semantics as JavaScript. Very large integers in your JSON lose precision, matching the TypeScript library's behavior. There is no `decimal` mode.

## Quick start

Populated in Phase 7. For now: see [`samples/ExpreszoDemo`](samples/ExpreszoDemo/) once implementation lands.

## Porting roadmap

| Phase | Scope |
|------:|-------|
| 0 | Repo bootstrap, CI, AOT canary |
| 1 | `Value` union, `Scope`, JsonDocument boundary, error hierarchy |
| 2 | Tokenizer |
| 3 | AST + Pratt parser |
| 4 | Evaluator + `Expression` API |
| 5 | All 107 operators and built-in functions |
| 6 | Validator, security, pluggable error handling |
| 7 | Public API polish, documentation, NuGet packaging |
| 8 | Coverage sweep, benchmarking, targeted optimization |

## Out of scope

- MCP server, language service (LSP), benchmarks — these live in `expreszo-typescript` and aren't ported.
- Legacy-mode semantics from the TypeScript library.
- Runtime disabling of specific operators.
- Operator customization (the built-in operator set is fixed; custom functions remain supported).

## License

MIT.
