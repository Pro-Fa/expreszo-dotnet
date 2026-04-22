# Contributing to Expreszo .NET

> **Audience:** Project contributors.

Thank you for your interest in contributing to Expreszo! This guide covers the development setup, project layout, and PR workflow.

## Development Setup

### Prerequisites

- **.NET 10 SDK** (10.0.200 or newer).
- For the AOT canary locally, you'll also need the platform C/C++ toolchain required by Native AOT — see [Microsoft's prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/). The canary is optional locally; CI runs it on Linux on every PR.

### Getting Started

```bash
git clone https://github.com/pro-fa/expreszo-dotnet.git
cd expreszo-dotnet

dotnet restore Expreszo.slnx
dotnet build Expreszo.slnx --configuration Release
dotnet run --project test/Expreszo.Tests/Expreszo.Tests.csproj --configuration Release --no-build
```

## Project Structure

```
expreszo-dotnet/
├── src/
│   └── Expreszo/
│       ├── Expreszo.csproj       # library project (AOT-enabled analyzers)
│       ├── Parser.cs             # public entry point
│       ├── Expression.cs         # parsed expression class
│       ├── Value.cs              # Value discriminated union
│       ├── Scope.cs              # layered evaluation scope
│       ├── EvalContext.cs
│       ├── OperatorTable.cs
│       ├── Ast/                  # AST node records + visitors
│       │   ├── Node.cs
│       │   ├── INodeVisitor.cs
│       │   └── Visitors/         # Simplify / Substitute / ToString / Symbols
│       ├── Parsing/              # Tokenizer + TokenCursor + PrattParser
│       ├── Evaluation/           # Evaluator (single ValueTask<Value> walker)
│       ├── Builtins/             # Operator / function presets
│       ├── Validation/           # ExpressionValidator
│       ├── Errors/               # Exception hierarchy + handlers
│       └── Json/                 # JsonBridge
├── test/
│   └── Expreszo.Tests/           # TUnit test project (+ NSubstitute)
├── samples/
│   ├── ExpreszoDemo/             # end-user demo
│   └── AotCheck/                 # AOT canary CI publishes with PublishAot=true
├── docs/                         # MkDocs site (this file lives here)
├── Directory.Build.props
├── Expreszo.slnx                 # SDK-style solution file
└── .github/workflows/ci.yml
```

## Development Workflow

### Build

```bash
dotnet build Expreszo.slnx --configuration Release
```

Warnings are treated as errors on the library project (`TreatWarningsAsErrors=true`). The AOT / trim analysers are on — code that needs dynamic code generation or untrimmable reflection will fail the build with `IL2026` / `IL3050`.

### Tests

Expreszo uses [TUnit](https://github.com/thomhurst/TUnit) on the Microsoft Testing Platform. `.NET 10` removed VSTest backward compat in `dotnet test`, so tests are run by executing the test project directly:

```bash
# Run all tests
dotnet run --project test/Expreszo.Tests/Expreszo.Tests.csproj --configuration Release --no-build

# Filter by tree node (class / method)
dotnet run --project test/Expreszo.Tests/Expreszo.Tests.csproj --configuration Release --no-build -- --treenode-filter "/*/*/TokenizerTests/*"

# Coverage (Cobertura XML under test/Expreszo.Tests/bin/.../TestResults/)
dotnet run --project test/Expreszo.Tests/Expreszo.Tests.csproj --configuration Release --no-build -- --coverage --coverage-output-format cobertura
```

Target is ≥80% line coverage.

### AOT canary (optional, locally)

```bash
dotnet publish samples/AotCheck/AotCheck.csproj \
    --configuration Release \
    --runtime <rid> --self-contained \
    -p:PublishAot=true

# e.g. win-x64 on Windows, linux-x64 on Linux, osx-arm64 on Apple silicon
./artifacts/Expreszo.AotCheck
```

CI runs this on every PR. Any warning in the library call graph fails the publish.

### Pack

```bash
dotnet pack src/Expreszo/Expreszo.csproj --configuration Release --output ./artifacts
```

Produces `Expreszo.X.Y.Z.nupkg` + `.snupkg`.

### Docs

Documentation lives under `docs/` and is built with [MkDocs](https://www.mkdocs.org/) + [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/).

```bash
pip install mkdocs mkdocs-material pymdown-extensions
mkdocs serve          # live preview at http://127.0.0.1:8000/
mkdocs build          # static site in site/
```

## Code Style

### General

- `file-scoped namespaces` (enforced by `.editorconfig`).
- 4-space indent for C#, 2-space for XML/JSON/YAML/Markdown.
- Nullable reference types enabled throughout.
- Prefer immutability: `sealed record` for data, `readonly` fields, `ImmutableArray<T>`, `FrozenDictionary<T>` when appropriate.

### Naming

- **Files**: `PascalCase.cs`.
- **Namespaces**: `PascalCase`, one per folder under `Expreszo.*`.
- **Classes / records / interfaces / enums**: `PascalCase`.
- **Methods / properties**: `PascalCase`.
- **Parameters / locals**: `camelCase`.
- **Private fields**: `_camelCase`.
- **Constants**: `PascalCase` (following BCL conventions, not `UPPER_SNAKE_CASE`).

### Example

```csharp
namespace Expreszo;

/// <summary>Brief one-liner.</summary>
public sealed class Thing
{
    private readonly int _count;

    public Thing(int count)
    {
        _count = count;
    }

    public bool TryDoSomething(string input, out int result)
    {
        // ...
    }
}
```

### XML docs

Every public member has `/// <summary>` at minimum. Longer descriptions go in `<remarks>`. Use `<paramref>` / `<see cref="...">` / `<list type="bullet">` as appropriate.

## Testing Guidelines

### Layout

- Test files mirror the source structure: `src/Expreszo/Parsing/*.cs` → `test/Expreszo.Tests/Parsing/*.cs`.
- Use TUnit's `[Test]` for single cases and `[Arguments(...)]` for parameterised cases.
- Prefer expression-driven tests (`Parser.Evaluate(...)`) over hand-building AST nodes — they stay readable and double as documentation.

### Example

```csharp
namespace Expreszo.Tests.Parsing;

public class TokenizerTests
{
    private static Token[] Tokenize(string expression)
    {
        var tokenizer = new Tokenizer(ParserConfig.Default, expression);
        var tokens = new List<Token>();
        while (true)
        {
            var t = tokenizer.Next();
            tokens.Add(t);
            if (t.Kind == TokenKind.Eof) break;
        }
        return [.. tokens];
    }

    [Test]
    [Arguments("0", 0d)]
    [Arguments("1", 1d)]
    [Arguments("42", 42d)]
    public async Task Tokenizes_decimal_numbers(string input, double expected)
    {
        var tokens = Tokenize(input);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[0].Number).IsEqualTo(expected);
    }
}
```

Test names can use `snake_case_for_readability` (CA1707 is suppressed in the test project).

## Pull Request Process

1. **Branch**

   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make changes**
   - Add or update tests for new behaviour.
   - Update docs for user-visible changes (syntax, public API, security).
   - Keep the existing code style.

3. **Run checks locally**

   ```bash
   dotnet build Expreszo.slnx --configuration Release
   dotnet run --project test/Expreszo.Tests/Expreszo.Tests.csproj --configuration Release --no-build
   ```

4. **Commit**

   Use [Conventional Commits](https://www.conventionalcommits.org/):

   - `feat:` — new features
   - `fix:` — bug fixes
   - `docs:` — documentation
   - `test:` — tests only
   - `refactor:` — no behaviour change
   - `perf:` — performance
   - `chore:` — tooling / build / ci

5. **Push and open a PR**

   CI will run build, tests, pack, and the AOT canary on Linux. All four must pass.

## Adding a Function

1. Pick the appropriate preset in `src/Expreszo/Builtins/` (or add a new one).
2. Register the function via `builder.AddFunction("name", impl)`. Use `OperatorTableBuilder.Sync(args => ...)` for synchronous functions; for async functions, return a `ValueTask<Value>` directly and pass `isAsync: true`.
3. If the function name is also reachable as a unary operator (e.g. trig functions), register it via `AddUnary` as well.
4. Add tests in `test/Expreszo.Tests/BuiltinsTests.cs` or the appropriate sub-file.
5. Document the function in [`docs/syntax.md`](syntax.md) — the user-facing language reference.

## Adding an Operator

1. Extend the tokenizer (`Parsing/Tokenizer.cs`) if the operator introduces a new symbolic form. Named operators (letters) go through `ParserConfig.DefaultUnaryOps` / `DefaultBinaryOps`.
2. Extend the Pratt parser (`Parsing/PrattParser.cs`) to handle the operator at the right precedence level.
3. Register the implementation in the appropriate preset (`Builtins/`).
4. Add AST-, parser-, and evaluator-level tests.
5. Document the operator in [`docs/syntax.md`](syntax.md).

## Questions?

- File an issue on [GitHub](https://github.com/pro-fa/expreszo-dotnet/issues).
- For design discussion before opening a PR, an issue with the `discussion` label works well.

Thanks for contributing!
