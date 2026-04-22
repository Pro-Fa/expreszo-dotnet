# Expression Quick Reference

> **Audience:** Users writing expressions in applications powered by Expreszo.

This is a quick reference card. For detailed documentation, see [Expression Syntax](syntax.md).

## Arithmetic

| Expression | Result | Description |
|:-----------|:-------|:------------|
| `2 + 3` | 5 | Addition |
| `10 - 4` | 6 | Subtraction |
| `3 * 4` | 12 | Multiplication |
| `15 / 3` | 5 | Division |
| `10 % 3` | 1 | Remainder (modulo) |
| `2 ^ 3` | 8 | Exponentiation |
| `5!` | 120 | Factorial |
| `-x` | negated | Negation |

## Comparison

| Expression | Result | Description |
|:-----------|:-------|:------------|
| `5 > 3` | true | Greater than |
| `5 >= 5` | true | Greater than or equal |
| `3 < 5` | true | Less than |
| `3 <= 3` | true | Less than or equal |
| `5 == 5` | true | Equal |
| `5 != 3` | true | Not equal |
| `"a" in ["a", "b"]` | true | In array |
| `"c" not in ["a", "b"]` | true | Not in array |

## Logic

| Expression | Result | Description |
|:-----------|:-------|:------------|
| `true and false` | false | Logical AND |
| `true or false` | true | Logical OR |
| `not true` | false | Logical NOT |

Short-circuits: `and` / `&&` only evaluate the right-hand side if the left is truthy; `or` / `||` only if the left is falsy.

## Conditionals

| Expression | Result | Description |
|:-----------|:-------|:------------|
| `x > 0 ? "yes" : "no"` | depends on x | Ternary (if-then-else) |
| `x ?? 0` | x or 0 | Coalesce (null / undefined / NaN / Infinity fallback) |
| `if(cond, a, b)` | lazy | Function form; only the chosen branch evaluates |

## Concatenation and Type Cast

| Expression | Result | Description |
|:-----------|:-------|:------------|
| `[1, 2] \| [3, 4]` | `[1, 2, 3, 4]` | Array concatenation |
| `"hi" \| " world"` | `"hi world"` | String concatenation (coerces either side) |
| `"42" as "number"` | `42` | Parse string to number |
| `3.7 as "int"` | `4` | Round to integer |
| `5 as "boolean"` | `true` | Coerce to boolean |

## Math Functions

| Function | Example | Result |
|:---------|:--------|:-------|
| `abs(x)` | `abs(-5)` | 5 |
| `round(x)` | `round(3.7)` | 4 |
| `floor(x)` | `floor(3.7)` | 3 |
| `ceil(x)` | `ceil(3.2)` | 4 |
| `sqrt(x)` | `sqrt(16)` | 4 |
| `min(a, b, ...)` | `min(3, 1, 4)` | 1 |
| `max(a, b, ...)` | `max(3, 1, 4)` | 4 |
| `clamp(x, min, max)` | `clamp(15, 0, 10)` | 10 |
| `pow(x, y)` | `pow(2, 3)` | 8 |
| `sin(x)` | `sin(PI / 2)` | 1 |
| `cos(x)` | `cos(0)` | 1 |
| `log(x)` | `log(E)` | 1 |
| `log10(x)` | `log10(100)` | 2 |
| `roundTo(x, n)` | `roundTo(1.23456, 2)` | 1.23 |

## Statistics

| Function | Example | Result |
|:---------|:--------|:-------|
| `sum(a)` | `sum([1, 2, 3])` | 6 |
| `mean(a)` | `mean([2, 4, 6])` | 4 |
| `median(a)` | `median([1, 2, 3, 4, 5])` | 3 |
| `variance(a)` | `variance([1, 2, 3])` | 0.666… |
| `stddev(a)` | `stddev([1, 2, 3])` | 0.816… |
| `percentile(a, p)` | `percentile([1, 2, 3, 4], 50)` | 2.5 |

## String Functions

| Function | Example | Result |
|:---------|:--------|:-------|
| `length(s)` | `length("hello")` | 5 |
| `toUpper(s)` | `toUpper("hi")` | `"HI"` |
| `toLower(s)` | `toLower("HI")` | `"hi"` |
| `trim(s)` | `trim("  x  ")` | `"x"` |
| `left(s, n)` | `left("hello", 2)` | `"he"` |
| `right(s, n)` | `right("hello", 2)` | `"lo"` |
| `contains(s, sub)` | `contains("hello", "ell")` | true |
| `startsWith(s, sub)` | `startsWith("hello", "he")` | true |
| `endsWith(s, sub)` | `endsWith("hello", "lo")` | true |
| `replace(s, old, new)` | `replace("aa", "a", "b")` | `"bb"` |
| `split(s, delim)` | `split("a,b", ",")` | `["a", "b"]` |
| `join(a, sep)` | `join([1, 2], "-")` | `"1-2"` |
| `base64Encode(s)` | `base64Encode("hi")` | `"aGk="` |
| `coalesce(...)` | `coalesce(null, "", "ok")` | `"ok"` |

## Array Functions

| Function | Example | Result |
|:---------|:--------|:-------|
| `count(arr)` | `count([1, 2, 3])` | 3 |
| `indexOf(arr, val)` | `indexOf([1, 2, 3], 2)` | 1 |
| `unique(arr)` | `unique([1, 1, 2])` | `[1, 2]` |
| `map(arr, fn)` | `map([1, 2], x => x * 2)` | `[2, 4]` |
| `filter(arr, fn)` | `filter([1, 2, 3], x => x > 1)` | `[2, 3]` |
| `find(arr, fn)` | `find([1, 5, 2], x => x > 3)` | 5 |
| `fold(arr, init, fn)` | `fold([1, 2, 3], 0, (a, x) => a + x)` | 6 |
| `some(arr, fn)` | `some([1, 5], x => x > 3)` | true |
| `every(arr, fn)` | `every([1, 2], x => x > 0)` | true |
| `range(start, end)` | `range(0, 5)` | `[0, 1, 2, 3, 4]` |
| `chunk(arr, size)` | `chunk([1, 2, 3, 4], 2)` | `[[1, 2], [3, 4]]` |
| `groupBy(arr, fn)` | `groupBy([1, 2, 3], x => x % 2)` | `{"1": [1, 3], "0": [2]}` |
| `sort(arr, cmp?)` | `sort([3, 1, 2])` | `[1, 2, 3]` |

## Object Functions

| Function | Example | Result |
|:---------|:--------|:-------|
| `keys(obj)` | `keys({a: 1, b: 2})` | `["a", "b"]` |
| `values(obj)` | `values({a: 1, b: 2})` | `[1, 2]` |
| `merge(o1, o2)` | `merge({a: 1}, {b: 2})` | `{a: 1, b: 2}` |
| `pick(obj, keys)` | `pick({a: 1, b: 2}, ["a"])` | `{a: 1}` |
| `omit(obj, keys)` | `omit({a: 1, b: 2}, ["b"])` | `{a: 1}` |
| `mapValues(obj, fn)` | `mapValues({a: 1}, v => v * 10)` | `{a: 10}` |

## Type Checking

| Function | Description |
|:---------|:------------|
| `isNumber(v)` | Returns true if v is a number |
| `isString(v)` | Returns true if v is a string |
| `isArray(v)` | Returns true if v is an array |
| `isObject(v)` | Returns true if v is an object |
| `isBoolean(v)` | Returns true if v is a boolean |
| `isNull(v)` | Returns true if v is null |
| `isUndefined(v)` | Returns true if v is undefined |
| `isFunction(v)` | Returns true if v is a function |

## Constants

| Constant | Value |
|:---------|:------|
| `PI` | 3.14159... |
| `E` | 2.71828... |
| `Infinity` | positive infinity |
| `NaN` | not-a-number |
| `true` | Boolean true |
| `false` | Boolean false |
| `null` | null |
| `undefined` | undefined |

## Array and Object Literals

```
[1, 2, 3]
["a", "b", "c"]
[1, "mixed", true, [nested]]
[1, ...rest, 5]                         // spread
```

```
{a: 1, b: 2}
{name: "John", age: 30}
{"my-key": 1, ...base}                  // quoted keys and spread
```

## Property Access

```
user.name                               // Object property (dot)
user.address.city                       // Nested property
items[0]                                // Array index
items[0].name                           // Combined
```

Missing properties return `undefined` instead of throwing.

## Arrow Functions

```
x => x * 2                              // Single parameter
(x, y) => x + y                         // Multiple parameters
(acc, x) => acc + x                     // For fold / reduce
() => 42                                // Zero parameters
```

## Assignment and Sequences

```
x = 5; x * 2                            // Assignment, then use
fn = x => x * 2; fn(3)                  // Function assignment
a = 1; b = 2; a + b                     // Sequence
```

## CASE Expressions

```
case status
  when "active"  then "✓"
  when "pending" then "…"
  else "?"
end

case
  when score >= 90 then "A"
  when score >= 80 then "B"
  else "F"
end
```
