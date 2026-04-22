# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/Pro-Fa/expreszo-dotnet/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Pro-Fa/expreszo-dotnet/releases/tag/v0.1.0
