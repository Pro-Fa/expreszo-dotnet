# Date / Time plugin

> **Audience:** Developers who want date/time support in an ExpresZo parser.

The core `Expreszo` package ships with no date/time functions. Date support lives
in a separate, optional NuGet package — [`Expreszo.DateTime`](https://www.nuget.org/packages/Expreszo.DateTime) —
that registers **76 functions** on a parser when you opt in. It is a faithful
port of the TypeScript [`@pro-fa/expreszo-datetime`](https://github.com/Pro-Fa/expreszo-typescript)
plugin, backed by the BCL (`DateTimeOffset` + `TimeZoneInfo`) with **no external
dependencies**, and is Native AOT and trim compatible like the core.

## Install

```sh
dotnet add package Expreszo.DateTime
```

`Expreszo` is brought in transitively, so you don't need to install it
separately (though it's harmless to be explicit). Targets `net10.0`.

## Register

Register the plugin when you build the parser with the `Parser.WithPlugins`
factory:

```csharp
using Expreszo;
using Expreszo.DateTimes;

var parser = Parser.WithPlugins([new ExpreszoDateTimePlugin()]);

parser.Evaluate("format(addDuration('2026-01-01', 7, 'days'), 'yyyy-MM-dd')");
// => Value.String "2026-01-08"
```

!!! note "Namespace vs. package id"
    The NuGet package and assembly are named **`Expreszo.DateTime`** (singular),
    but the code namespace is **`Expreszo.DateTimes`** (plural). A namespace
    ending in `DateTime` would shadow `System.DateTime`, so the plural form is
    used in code while the package keeps the singular, familiar name.

Equivalently, you can pass plugins through `ParserOptions` — useful when you are
already constructing options:

```csharp
var parser = new Parser(new ParserOptions
{
    Plugins = [new ExpreszoDateTimePlugin()],
});
```

A `Parser` is immutable and thread-safe once built; construct it once and reuse
it across threads and evaluations.

### Writing your own plugin

The same surface is public, so any package can contribute functions:

```csharp
public sealed class MyPlugin : IExpreszoPlugin
{
    public string Name => "my-company/my-plugin";
    public string Version => "1.0.0";

    public void Register(IPluginRegistration r) =>
        r.AddFunction("double", (args, _) =>
            ValueTask.FromResult<Value>(Value.Number.Of(((Value.Number)args[0]).V * 2)));
}
```

Registration is explicit (no reflection), which keeps plugin wiring AOT- and
trim-safe.

## Configuring the clock and zone

Impure functions (`now`, `today`, `age`, the distance and relative-to-now
helpers) read a clock, and zone-less values are interpreted in a "local" zone.
Both are configurable via `DateTimeOptions` — most useful for **deterministic
tests** and for pinning a server zone:

```csharp
var plugin = new ExpreszoDateTimePlugin(new DateTimeOptions
{
    NowProvider = () => DateTimeOffset.Parse("2026-04-15T12:00:00Z"), // fixed clock
    DefaultZone = TimeZoneInfo.Utc,                                   // "local" zone
});

var parser = Parser.WithPlugins([plugin]);
parser.Evaluate("daysUntil('2026-04-20T12:00:00Z')"); // => 5
```

| Option | Default | Purpose |
| --- | --- | --- |
| `NowProvider` | `() => DateTimeOffset.Now` | Source of "now" for impure functions. |
| `DefaultZone` | `TimeZoneInfo.Local` | Zone for `now`/`today`/`time`, parsers/constructors with no explicit zone, and inspector rendering. |

## Input shapes

A date argument accepts any of these shapes, normalised at the boundary:

| Shape | Example in an expression |
| --- | --- |
| A `Value.DateTime` (produced by another datetime function) | `addDuration(now(), 1, 'days')` |
| ISO 8601 string | `'2026-01-01T00:00:00Z'` |
| Unix millisecond number | `1767225600000` |
| Native `System.DateTime` / `DateTimeOffset` (via a variable) | `year(d)` with `d` bound to a CLR date |

Functions that **produce** a date return a `Value.DateTime`, so chains stay
efficient. Functions that produce text (`format`, `toISO`, `toRelative`) or
numbers (`year`, `diff`, `toMillis`) return those directly.

### Passing native CLR dates as variables

`JsonDocument` input can't carry a native date, so native
`System.DateTime`/`DateTimeOffset` values enter through the variable resolver.
The package ships a helper that builds one:

```csharp
using Expreszo.DateTimes;

var vars = DateTimeVariables.FromObjects(
    new Dictionary<string, object?> { ["d"] = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });

parser.Evaluate("format(addDuration(d, 7, 'days'), 'yyyy-MM-dd')", values: null, resolver: vars);
// => "2026-01-08"
```

`FromObjects` maps `DateTimeOffset`/`DateTime` to `Value.DateTime` and other
scalars (`string`, `bool`, numeric) to their natural value kinds. For ad-hoc
conversion outside an expression, `Normalize.ToDateTime` /
`Normalize.ToDateTimeOrUndefined` are public too.

## Operators on DateTime values

DateTime values flow through the core comparison operators, comparing by instant
(in Unix milliseconds, matching the TypeScript `valueOf` semantics):

```
parseISO('2026-01-01') == parseISO('2026-01-01')   // true
parseISO('2026-01-01') <  parseISO('2026-02-01')   // true
parseISO('2026-01-01') >= parseISO('2026-01-01')   // true
```

A date also coerces to its Unix-millisecond value in numeric contexts
(`x as 'number'`, mixed `<`/`>` with a number). Arithmetic operators (`+`, `-`)
are intentionally **not** date-aware — use `addDuration`, `subtractDuration`,
and `diff`, which are explicit about units.

## JSON output

A date result serializes to an ISO 8601 string (JSON has no native date type):

```csharp
using Expreszo.Json;

Value r = parser.Evaluate("parseISO('2026-01-01T00:00:00Z')");
JsonBridge.ToJsonString(r); // => "\"2026-01-01T00:00:00.000+00:00\""
```

## Function reference

Units accepted by `addDuration`/`subtractDuration`/`startOf`/`endOf`/`diff`/`dateRange`
are: `year(s)`, `quarter(s)`, `month(s)`, `week(s)`, `day(s)`, `hour(s)`,
`minute(s)`, `second(s)`, `millisecond(s)` (singular or plural).

### Construction

| Function | Returns | Notes |
| --- | --- | --- |
| `now()` | DateTime | Current instant. |
| `today()` | DateTime | Start of the current day. |
| `yesterday()` / `tomorrow()` | DateTime | Start of the day before / after. |
| `parseISO(iso)` | DateTime | Parse an ISO 8601 string. |
| `parseDate(input, format, zone?)` | DateTime | Parse with a Luxon-style format token; optional IANA zone. |
| `fromMillis(ms)` / `fromUnix(seconds)` | DateTime | From a Unix timestamp. |
| `dateTime(y, m, d, h?, mi?, s?)` | DateTime | From numeric components. |
| `date(y, m, d)` | DateTime | Midnight from year/month/day. |
| `time(h, mi, s?, ms?)` | DateTime | Today at the given clock time. |

### Inspection — calendar parts

`year`, `month`, `day`, `hour`, `minute`, `second`, `millisecond`,
`dayOfWeek` (1 = Mon … 7 = Sun), `dayOfYear`, `weekOfYear` (ISO),
`daysInMonth`, `quarter` (1–4), `isoWeekYear`, `isLeapYear`, `daysInYear`,
`weeksInYear`, `isDST`, `offsetMinutes`, `offsetHours`, `zoneName`,
`isWeekend`, `isWeekday`, `isValid`.

### Inspection — relative to now

`isToday`, `isYesterday`, `isTomorrow`, `isThisWeek`, `isThisMonth`,
`isThisYear`, `isInPast`, `isInFuture`, `age` (whole years; 0 for future dates).

### Arithmetic

| Function | Returns | Notes |
| --- | --- | --- |
| `addDuration(d, amount, unit)` | DateTime | |
| `subtractDuration(d, amount, unit)` | DateTime | |
| `startOf(d, unit)` / `endOf(d, unit)` | DateTime | Truncate to the unit boundary. |
| `diff(d1, d2, unit)` | number | `d1 - d2` in the unit. |
| `clampDate(d, low, high)` | DateTime | Clamp into `[low, high]`. |
| `minDate(...)` / `maxDate(...)` | DateTime | Earliest / latest of N dates. |

### Comparison

| Function | Returns | Notes |
| --- | --- | --- |
| `isBefore(d1, d2)` / `isAfter(d1, d2)` | boolean | |
| `isSame(d1, d2, unit?)` | boolean | Exact equality, or truncated to a unit. |
| `isBetween(d, start, end, inclusive?)` | boolean | `inclusive` defaults to `true`. |
| `compareDates(d1, d2)` | number | `-1` / `0` / `1` — usable as a sort key. |
| `overlapsRange(s1, e1, s2, e2)` | boolean | |
| `containsDate(start, end, d)` | boolean | |

### Range / sequence

| Function | Returns | Notes |
| --- | --- | --- |
| `dateRange(start, end, unit, step?)` | array of DateTime | Half-open `[start, end)`; `step` defaults to 1. |
| `businessDaysBetween(start, end)` | number | Count of Mon–Fri in `[start, end)`. |
| `weekdaysBetween(start, end, weekday)` | number | Count of a given weekday (1–7) in `[start, end)`. |

### Distance from now

`daysUntil`, `daysSince`, `hoursUntil`, `hoursSince`, `minutesUntil`,
`minutesSince` — whole units, truncated toward zero.

### Format / zone

| Function | Returns | Notes |
| --- | --- | --- |
| `format(d, pattern)` | string | Luxon-style format tokens (`yyyy-MM-dd`, `MMM d, yyyy`, …). |
| `toISO(d)` | string | ISO 8601. |
| `toMillis(d)` / `toUnix(d)` | number | Unix milliseconds / seconds. |
| `setZone(d, zone)` | DateTime | Re-zone (`'utc'`, `'local'`, or an IANA id). |
| `toUTC(d)` / `toLocal(d)` | DateTime | Sugar for `setZone(d, 'utc' \| 'local')`. |
| `toRelative(d, base?)` | string | e.g. `"in 5 days"`. |
| `toRelativeCalendar(d, base?)` | string | e.g. `"tomorrow"`. |

## Notes and caveats

- **Default zone:** in production `DefaultZone` is `TimeZoneInfo.Local`. Pin it
  to `TimeZoneInfo.Utc` (and a fixed `NowProvider`) in tests for determinism.
- **IANA zone ids** (`'America/New_York'`) require ICU, which is the default on
  .NET 6+ across Windows/Linux/macOS.
- **Fractional calendar amounts** (e.g. `0.5` months) are rejected; use whole
  amounts for `year`/`quarter`/`month` units.
- `toRelative` / `toRelativeCalendar` produce English strings and are not
  locale-aware. `diff` for `month`/`quarter`/`year` uses a calendar-fractional
  algorithm.
