# Expreszo.DateTime

Optional date/time functions for the [Expreszo](https://www.nuget.org/packages/Expreszo)
expression evaluator — 76 functions covering parsing, formatting, arithmetic,
comparison, ranges, and relative-to-now helpers. Backed by the BCL
(`DateTimeOffset` + `TimeZoneInfo`) with **no external dependencies**, and Native
AOT / trim compatible. A faithful port of the TypeScript
[`@pro-fa/expreszo-datetime`](https://github.com/Pro-Fa/expreszo-typescript) plugin.

## Install

```sh
dotnet add package Expreszo.DateTime
```

`Expreszo` comes in transitively. Targets `net10.0`.

## Register

```csharp
using Expreszo;
using Expreszo.DateTimes;

var parser = Parser.WithPlugins([new ExpreszoDateTimePlugin()]);

parser.Evaluate("format(addDuration('2026-01-01', 7, 'days'), 'yyyy-MM-dd')");
// => Value.String "2026-01-08"
```

> The package/assembly is `Expreszo.DateTime`; the code namespace is
> `Expreszo.DateTimes` (plural) so it doesn't shadow `System.DateTime`.

## Configure the clock and zone

Impure functions (`now`, `today`, `age`, distance/relative helpers) read a clock,
and zone-less values use a "local" zone — both configurable, which is handy for
deterministic tests:

```csharp
var plugin = new ExpreszoDateTimePlugin(new DateTimeOptions
{
    NowProvider = () => DateTimeOffset.Parse("2026-04-15T12:00:00Z"),
    DefaultZone = TimeZoneInfo.Utc,
});
var parser = Parser.WithPlugins([plugin]);
parser.Evaluate("daysUntil('2026-04-20T12:00:00Z')"); // => 5
```

## Input shapes

Date arguments accept a `Value.DateTime`, an ISO 8601 string, a Unix-millisecond
number, or a native `System.DateTime`/`DateTimeOffset` passed via the variable
resolver (`DateTimeVariables.FromObjects`). DateTime values also flow through the
core comparison operators (`==`, `<`, `>=`, …) by instant.

## Documentation

Full guide and function reference:
<https://pro-fa.github.io/expreszo-dotnet/datetime-plugin/>
