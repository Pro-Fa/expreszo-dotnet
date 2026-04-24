# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.1] - 2026-04-24

### Changed

- `Expression.Root` is now a public property (was internal). External
  tooling consumers — in particular the `Expreszo.Analysis` package —
  need to pass the root `Node` into analysers like `TypeInference.Run`
  and `TypeValidator.Validate` without a second parse pass. No other
  changes; no breaking changes.

## [0.4.0] - 2026-04-24

Splits the language-server analyser out of the OmniSharp-coupled server
project into a new `Expreszo.Analysis` NuGet package. Tooling consumers
that want type inference, the built-in catalogue, and symbol / cursor
lookup can now take a single AOT-safe package without the OmniSharp +
MediatR transitive stack the LSP server requires. The server itself is
also NuGet-published this release for editor integrators who want to
embed it in-process.

### Added

- **New package `Expreszo.Analysis`** — AOT-compatible, `IsTrimmable=true`.
  Public surface promoted from language-server internals:
  - `BuiltinMetadata` + `BuiltinEntry` + `BuiltinKind` — catalogue of 27
    operators, 82 built-in functions, and 9 keyword / literal names,
    each annotated with return kind, min/max arity, and parameter-kind
    hints where the signature is precise enough to matter.
  - `TypeInference` — literal-driven, flow-insensitive per-node
    `ValueKind` annotation. Stays conservative: free identifiers and
    short-circuit operators resolve to `Unknown` so the validator only
    fires on genuinely concrete kinds.
  - `TypeValidator` — emits `SemanticException` diagnostics for
    non-numeric `+`, unsupported `as` targets, literal divide-by-zero,
    built-in arity mismatches, wrong-kind arguments, and dead-branch
    `isXxx(literal)` predicates.
  - `SymbolIndex` — name-keyed definition / reference spans from one
    AST walk.
  - `AstLocator` + `LocateResult` — deepest-node-at-offset lookup with
    ancestor chain.
  - `LineIndex` — offset ↔ (line, character) conversion; LSP-compatible
    position encoding.
- **New package `Expreszo.LanguageServer`** — the existing
  OmniSharp-based server promoted from project-only to a NuGet package.
  Public entry point is `ExpreszoLanguageServer.RunAsync(Stream, Stream,
  CancellationToken)`. Not AOT-compatible; use `Expreszo.Analysis`
  instead when you only want the analyser.
- Release workflow now packs all three publishable projects in one
  step; the existing `dotnet nuget push ./artifacts/*.nupkg` glob picks
  up every `.nupkg` / `.snupkg`.

### Changed

- The analyser source files (`BuiltinMetadata`, `TypeOverrides`,
  `TypeInference`, `TypeValidator`, `SymbolIndex`, `AstLocator`,
  `LineIndex`) moved from `Expreszo.LanguageServer` to the new
  `Expreszo.Analysis` assembly and switched namespace from
  `Expreszo.LanguageServer` to `Expreszo.Analysis`. The LSP server
  continues to consume them via a project reference.
- Handler source files add `using Expreszo.Analysis;`. No functional
  change; the LSP transport behaviour is unchanged.

### Notes

The core `Expreszo` package's AOT / trim guarantees are unchanged; the
`AotCheck` sample still publishes and runs natively on every tag. The
new `Expreszo.Analysis` package joins the AOT-safe tier. Only
`Expreszo.LanguageServer` is AOT-incompatible, which is by design given
the OmniSharp framework it embeds.

## [0.3.0] - 2026-04-24

The "tooling" release. A full Language Server Protocol implementation
ships alongside the library, plus small additions to the library's public
surface that the server (and any other diagnostic-driven tool) can use
directly.

### Added

- `Parser.TryParse(string)` — error-recovering parse entry point.
  Splits the source on top-level semicolons and parses each statement
  independently, so a syntax error in one statement no longer prevents
  the others from producing AST or diagnostics. Returns a `ParseResult`
  with a walkable `Expression` plus an
  `ImmutableArray<ExpressionException>` of collected errors.
  Existing throwing `Parser.Parse` is unchanged.
- `Expreszo.ValueKind` — coarse type tag (Number / String / Boolean /
  Null / Undefined / Array / Object / Function / Unknown) matching the
  variants of `Value`. Intended for tooling that wants to annotate AST
  nodes; deliberately flat (no unions / generics) to match the
  literal-driven analysis pass the server performs.
- `Expreszo.Errors.SemanticException` — sealed `ExpressionException`
  subclass for statically-detectable runtime-type problems. The library
  itself never throws it; consumers producing their own type analyses
  can now emit diagnostics through the shared error hierarchy.
- `src/Expreszo.LanguageServer/` and `src/Expreszo.LanguageServer.Host/`
  — Language Server Protocol implementation + stdio host binary
  (`expreszo-lsp`), built on `OmniSharp.Extensions.LanguageServer`.
  Capabilities:
  - Diagnostics (syntax errors from `TryParse`; semantic warnings from a
    literal-driven type validator covering non-numeric `+`, unsupported
    `as` targets, literal divide-by-zero, built-in arity mismatches,
    wrong-kind argument passing, and dead-branch `isXxx(literal)`
    predicates).
  - Hover with Markdown signatures for every built-in, operator, and
    keyword.
  - Completion for the built-in catalogue plus scope-local names.
  - Signature help with parameter info for built-ins that have declared
    shapes.
  - Document symbols (top-level `FunctionDef`s and assignments).
  - Goto-definition, find-references, rename (local identifiers; refuses
    to rename built-ins or accept invalid identifier targets).
  - Semantic tokens driven by a real tokenizer pass so highlighting
    keeps working on incomplete input.
- `test/Expreszo.LanguageServer.Tests/` — TUnit test project covering
  each provider + a transport smoke test that drives the server over
  in-process pipes.

### Changed

- Release workflow publishes to NuGet.org via Trusted Publishing (OIDC
  token exchange) instead of a long-lived `NUGET_API_KEY` secret.
- `README.md`: Language Server is no longer listed under "Out of scope";
  a new "Tooling" section points at the new projects.
- CI workflow runs the LSP test project in addition to the library
  tests.

### Notes on library AOT / trimming

Only the library (`src/Expreszo/`) is published to NuGet. The LSP
projects depend on `OmniSharp.Extensions.LanguageServer`, which wires
handlers through reflection and is therefore not AOT-compatible. The
library's `IsAotCompatible=true` / `IsTrimmable=true` guarantees are
unchanged, and the `AotCheck` sample in the release workflow still
publishes and runs natively on every tag.

## [0.2.1] - 2026-04-22

Release-pipeline validation version. No library changes over 0.2.0.

### Changed

- CHANGELOG now documents the 0.2.0 refactor.

## [0.2.0] - 2026-04-22

Internal refactor - no behavior changes, all 391 tests still pass.

### Changed

- Broke up long methods across the tokenizer, Pratt parser, evaluator,
  and built-in presets into focused helpers (`ScanMantissa`/`ScanExponent`,
  `BuildAssignment`, `EvalAssignment`/`EvalShortCircuit`/`EvalGenericBinary`,
  `Emit*` visitors, `NumericBinary`/`Add`/`Divide`/`Concat`,
  `TryResolveArrayFunctionPair`).
- Preferred switch expressions over chained `if`/`is` patterns throughout
  the parser, evaluator, and `ToStringVisitor`.

## [0.1.0] - 2026-04-22

Initial public release.

### Added

- Pratt parser producing an immutable AST, with 27 operators and 82 built-in
  functions across Math, Array, String, Object, Utility, and type-check presets.
- `JsonDocument`-based I/O: variables in, results out, round-tripped through
  `System.Text.Json` primitives with no reflection.
- Single async-capable evaluator (`ValueTask<Value>`) with a synchronous fast
  path and a `CancellationToken` honoured on every call boundary.
- Dynamic `VariableResolver` fallback for identifiers the scope did not bind.
- Native AOT and trim compatibility: `IsAotCompatible=true`,
  `IsTrimmable=true`, and every AOT/trim analyzer enabled. CI verifies on
  every PR by publishing and running the `AotCheck` sample with
  `PublishAot=true`.
- Security hardening:
  - `__proto__`, `prototype`, `constructor` rejected on member, bracket, and
    scope paths.
  - Recursion capped at 256 levels of nested calls and 256 levels of parse
    depth.
  - Output-size and input-range budgets for resource-heavy built-ins
    (`repeat`, `padLeft`/`Right`/`Both`, `range`, `fac`, postfix `!`),
    published as `Expreszo.EvaluationLimits` constants.
  - Identity-based allow-list that rejects raw CLR delegates smuggled in
    through a custom `VariableResolver` or `Scope` binding.
  - `ExpressionValidator` for pre-evaluation static checks.
- BenchmarkDotNet suite covering parsing, evaluation, simplification, and
  end-to-end parse+evaluate cycles.
- MkDocs documentation site with syntax, parser, evaluation, security,
  AOT/trimming, and contributing guides.
- GitHub Actions workflows: `ci.yml` (build + test + AOT canary + pack) and
  `release.yml` (tag-driven NuGet publish with MinVer + GitHub Release).

[Unreleased]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Pro-Fa/expreszo-dotnet/releases/tag/v0.1.0
