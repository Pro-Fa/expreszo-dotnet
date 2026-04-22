# AOT & Trimming

> **Audience:** Developers using Native AOT, IL trimming, or single-file publishing — and anyone who wants to understand how Expreszo keeps itself out of your `IL2026` / `IL3050` warning budget.

## What the library guarantees

The `Expreszo` assembly is built with every relevant analyser enabled:

```xml
<IsAotCompatible>true</IsAotCompatible>
<IsTrimmable>true</IsTrimmable>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
<EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
```

If any code path in the library needs dynamic code generation or untrimmable reflection, the build fails **here**, not in your app. CI runs a dedicated job that does:

```
dotnet publish samples/AotCheck --configuration Release \
    --runtime linux-x64 --self-contained -p:PublishAot=true
```

and executes the resulting native binary. The canary exercises the Parser, Expression, evaluator, validator, JsonBridge, and every built-in preset — so a regression anywhere in the call graph fails the publish step.

There are no `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` attributes in the production assembly.

## What you get

Enable AOT in your own project with no additional configuration:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization> <!-- optional; reduces binary size -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Expreszo" Version="1.0.*" />
  </ItemGroup>
</Project>
```

```
dotnet publish -r linux-x64 -c Release
```

## What's forbidden in the library source

To maintain the guarantee above, Expreszo's production code **never** uses:

- `System.Reflection.Emit` (`DynamicMethod`, `ILGenerator`, `TypeBuilder`, etc.)
- `System.Linq.Expressions.Expression.Compile()`
- `Activator.CreateInstance(Type)` with runtime-supplied types
- `Type.GetType(string)`
- `Assembly.GetTypes()` scanning
- `System.Text.Json.JsonSerializer` reflection overloads (no `JsonSerializerContext`, no reflection-based (de)serialisation)
- The `dynamic` keyword
- `MakeGenericType` / `MakeGenericMethod` with user-supplied parameters

In place of these, Expreszo uses:

- **Explicit descriptor tables** for operators and functions (`OperatorTable`, `OperatorTableBuilder`).
- **Manual JsonElement traversal** via `GetProperty`, `EnumerateArray`, `EnumerateObject`, `GetDouble`, etc.
- **`Utf8JsonWriter`** for serialisation (no `JsonSerializer`).
- **`FrozenDictionary<string, T>`, `ImmutableArray<T>`, `ReadOnlySpan<T>`** for fast lookups and zero-copy slicing.

## What this means for your code

- Customs functions you register are plain `ExprFunc` delegates — they're AOT-safe by construction.
- A `VariableResolver` is a delegate too; capturing state in a closure is fine.
- `JsonDocument` / `JsonElement` / `Utf8JsonWriter` are AOT-safe; Expreszo never goes behind them to use reflection.

If you build an abstraction on top of Expreszo, follow the same rules — anywhere you'd normally reach for `JsonSerializer.Deserialize<T>` to convert a `JsonElement` to a typed .NET object, prefer explicit code paths that walk the element.

## Trimming

The library is marked `IsTrimmable`, so the linker can drop unused code paths when your app publishes with `<PublishTrimmed>true</PublishTrimmed>`. CI runs a trimmed publish of the sample alongside the AOT publish:

```
dotnet publish samples/AotCheck --configuration Release \
    --runtime linux-x64 -p:PublishTrimmed=true
```

No `IL2026` (unreferenced-code) or `IL3050` (dynamic-code) warnings should surface for code that imports `Expreszo`.

## Single-file publish

Enabling single-file publish (`<PublishSingleFile>true</PublishSingleFile>`) likewise doesn't surface any `IL3000` warnings for Expreszo code — no `Assembly.Location` usage, no `Assembly.CodeBase`, no assembly loading.

## If you hit a warning

If your app's trim / AOT build surfaces a warning that points inside the Expreszo assembly, that's a bug — please [open an issue](https://github.com/pro-fa/expreszo-dotnet/issues) with:

- The warning code (`IL2026`, `IL2070`, `IL3050`, etc.)
- The call site (stack frame from the analyser message)
- Your project's `<PublishAot>` / `<PublishTrimmed>` / `<PublishSingleFile>` settings

The AOT canary in CI is meant to catch these upstream, but new .NET SDK versions sometimes tighten the analysers, so regressions are possible.

## See Also

- [Values & JsonDocument](values-and-json.md) — the AOT-safe JSON bridge.
- [Security & Validation](security.md) — the "no runtime dynamism" story extends to security too.
- Microsoft's [Native AOT docs](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) for general background.
