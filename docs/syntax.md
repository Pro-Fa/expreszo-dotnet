# Expression Syntax

> **Audience:** Users writing expressions in applications powered by ExpresZo.

The expression language is similar to JavaScript but more math-oriented. For example, the `^` operator is exponentiation, not xor.

## Operator Precedence

Operators are listed from highest to lowest precedence:

| Operator                 | Associativity | Description |
|:------------------------ |:------------- |:----------- |
| `(...)`                  | None          | Grouping |
| `f()`, `x.y`, `a[i]`     | Left          | Function call, property access, array indexing |
| `!`                      | Left          | Factorial (postfix) |
| `^`                      | Right         | Exponentiation |
| `+`, `-`, `not`, `sqrt`, ... | Right     | Unary prefix operators (see below) |
| `*`, `/`, `%`            | Left          | Multiplication, division, remainder |
| `+`, `-`, `\|`           | Left          | Addition, subtraction, array / string concatenation |
| `??`, `as`               | Left          | Null coalesce, type cast |
| `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `not in` | Left | Comparison |
| `and`, `&&`              | Left          | Logical AND (short-circuits) |
| `or`, `\|\|`             | Left          | Logical OR (short-circuits) |
| `x ? y : z`              | Right         | Ternary conditional |
| `=>`                     | Right         | Arrow function |
| `=`                      | Right         | Variable assignment |
| `;`                      | Left          | Statement separator |

## Literals

| Form | Examples |
|:-----|:---------|
| Decimal | `0`, `42`, `3.14`, `.5`, `1e10`, `2.5e-3` |
| Hex | `0xFF`, `0xCAFE` |
| Binary | `0b1010`, `0b1111` |
| String | `"hi"`, `'hi'`, `"escape: \\n \\t \\uXXXX \\\""` |
| Boolean | `true`, `false` |
| Empty | `null`, `undefined` |

Strings accept `\n`, `\t`, `\r`, `\b`, `\f`, `\/`, `\\`, `\'`, `\"`, and `\uXXXX` unicode escapes. The `\u` form requires exactly four hex digits.

## Concatenation Operator

The `|` (pipe) operator concatenates arrays or strings:

- If both operands are arrays, they are concatenated as arrays.
- If both operands are strings, they are concatenated as strings.
- If either operand is a string, the other is coerced to a string and both are concatenated.

```
[1, 2] | [3, 4]           → [1, 2, 3, 4]
[1] | [2] | [3]           → [1, 2, 3]
"hello" | " " | "world"   → "hello world"
"Count: " | 42            → "Count: 42"
```

The `+` operator requires both operands to be numbers. String concatenation uses `|`.

## Unary Operators

The parser has several built-in "functions" that are actually unary operators. Unlike regular functions they accept exactly one argument, and parentheses are optional. With parentheses they have the same precedence as function calls; without parentheses they keep their normal unary precedence (just below `^`). For example, `sin(x)^2` is equivalent to `(sin x)^2`, and `sin x^2` is equivalent to `sin(x^2)`.

The unary `+` and `-` operators are an exception: they always have their normal precedence.

| Operator | Description |
|:-------- |:----------- |
| `-x`     | Negation |
| `+x`     | Unary plus. Converts the operand to a number; returns `undefined` if the result is `NaN`. |
| `x!`     | Factorial (x * (x-1) * ... * 2 * 1) for non-negative integers. |
| `abs x`  | Absolute value |
| `acos x` | Arc cosine (radians) |
| `acosh x`| Hyperbolic arc cosine |
| `asin x` | Arc sine (radians) |
| `asinh x`| Hyperbolic arc sine |
| `atan x` | Arc tangent (radians) |
| `atanh x`| Hyperbolic arc tangent |
| `cbrt x` | Cube root |
| `ceil x` | Ceiling - smallest integer >= x |
| `cos x`  | Cosine (radians) |
| `cosh x` | Hyperbolic cosine |
| `exp x`  | e^x |
| `expm1 x`| e^x - 1 |
| `floor x`| Floor - largest integer <= x |
| `length x`| String or array length |
| `ln x`   | Natural logarithm |
| `log x`  | Natural logarithm (synonym for `ln`) |
| `log10 x`| Base-10 logarithm |
| `log2 x` | Base-2 logarithm |
| `log1p x`| Natural logarithm of (1 + x) |
| `not x`  | Logical NOT |
| `round x`| Rounded to nearest integer (away-from-zero on ties) |
| `sign x` | -1, 0, or 1 |
| `sin x`  | Sine (radians) |
| `sinh x` | Hyperbolic sine |
| `sqrt x` | Square root - returns `NaN` if x is negative |
| `tan x`  | Tangent (radians) |
| `tanh x` | Hyperbolic tangent |
| `trunc x`| Integer part - floor for positives, ceiling for negatives |

## Pre-defined Functions

Besides the unary operators, several pre-defined functions are available. Your application may register additional functions.

### Numeric Functions

| Function | Description |
|:---------|:----------- |
| `random(n)` | Random number in `[0, n)`. Defaults to `n = 1`. |
| `fac(n)` | Factorial. |
| `min(a, b, ...)` | Minimum. Accepts variadic numbers or a single array. |
| `max(a, b, ...)` | Maximum. Accepts variadic numbers or a single array. |
| `clamp(x, min, max)` | Clamp `x` to `[min, max]`. |
| `hypot(a, b, ...)` | √(a² + b² + ...) |
| `pow(x, y)` | x^y. |
| `gamma(n)` | Gamma function (Lanczos approximation). |
| `atan2(y, x)` | Arc tangent of y / x - the angle from the origin to `(x, y)` in radians. |
| `roundTo(x, n)` | Round to `n` decimal places. |

### Statistics Functions

| Function | Description |
|:---------|:----------- |
| `sum(a)` | Sum of array elements. |
| `mean(a)` | Arithmetic mean. |
| `median(a)` | Median. |
| `mostFrequent(a)` | Mode - the most frequently occurring value. |
| `variance(a)` | Population variance. |
| `stddev(a)` | Population standard deviation. |
| `percentile(a, p)` | p-th percentile (0–100), linear interpolation. |

### Array Functions

| Function | Description |
|:---------|:----------- |
| `count(a)` | Array length. |
| `map(a, fn)` | Array map - pass each element (and index) to `fn`, collect results. |
| `filter(a, fn)` | Keep elements where `fn(x, i)` is truthy. |
| `fold(a, init, fn)` | Left fold: `acc = fn(acc, x, i)`. |
| `reduce(a, init, fn)` | Alias for `fold`. |
| `find(a, fn)` | First element satisfying `fn(x, i)`, else `undefined`. |
| `some(a, fn)` | `true` if any element satisfies `fn`. |
| `every(a, fn)` | `true` if all elements satisfy `fn`. Empty arrays return `true`. |
| `unique(a)` / `distinct(a)` | Remove duplicates (strict equality). |
| `indexOf(a, x)` | First index of `x` in `a`, or `-1`. |
| `join(a, sep)` | Join elements with separator. |
| `range(start, end, step?)` | `[start, start+step, ...]` while `< end` (or `> end` if step is negative). |
| `chunk(a, size)` | Split into sub-arrays of `size`. |
| `union(a, b, ...)` | Concatenate arrays and deduplicate, preserving first-seen order. |
| `intersect(a, b, ...)` | Elements in all input arrays. |
| `groupBy(a, fn)` | Group elements by `fn(x, i)` key. Returns an object. |
| `countBy(a, fn)` | Count elements by `fn(x, i)` key. Returns an object of counts. |
| `sort(a, fn?)` | Sort. With comparator `(a, b) => number` for custom order. |
| `flatten(a, depth?)` | Flatten nested arrays (default depth 1). |
| `naturalSort(a)` | Sort strings with alphanumeric-aware comparison (`"file2"` before `"file10"`). |

Higher-order functions accept both `(array, fn)` and `(fn, array)` argument orders for back-compat with upstream libraries.

### Utility Functions

| Function | Description |
|:---------|:----------- |
| `if(c, a, b)` | Function form of `c ? a : b`. **Lazy** - only the matching branch is evaluated. |
| `coalesce(a, b, ...)` | First non-null, non-undefined, non-empty-string value. |
| `json(value)` | Convert to a JSON string. |

### Type Checking Functions

| Function | Description |
|:---------|:----------- |
| `isArray(v)` | True if `v` is an array. |
| `isObject(v)` | True if `v` is an object (not null, not array). |
| `isNumber(v)` | True if `v` is a number. |
| `isString(v)` | True if `v` is a string. |
| `isBoolean(v)` | True if `v` is a boolean. |
| `isNull(v)` | True if `v` is `null`. |
| `isUndefined(v)` | True if `v` is `undefined`. |
| `isFunction(v)` | True if `v` is a function (lambda or built-in). |

## String Functions

### Inspection

| Function | Description |
|:---------|:----------- |
| `length(str)` | Length of a string. Also works on arrays and numbers. |
| `isEmpty(str)` | `true` if length is 0; `undefined` if the value isn't a string. |
| `contains(str, substr)` | Substring check. Also works on arrays (element check). |
| `startsWith(str, substr)` | Prefix check. |
| `endsWith(str, substr)` | Suffix check. |
| `searchCount(str, substr)` | Non-overlapping occurrence count. |

### Transformation

| Function | Description |
|:---------|:----------- |
| `trim(str, chars?)` | Remove whitespace or specified characters from both ends. |
| `toUpper(str)` | Uppercase (invariant culture). |
| `toLower(str)` | Lowercase (invariant culture). |
| `toTitle(str)` | Title case (first letter of each word, invariant). |
| `repeat(str, n)` | Repeat `n` times. `n` must be a non-negative integer. |
| `reverse(str)` | Reverse a string or array. |

### Extraction

| Function | Description |
|:---------|:----------- |
| `left(str, n)` | Leftmost `n` characters. |
| `right(str, n)` | Rightmost `n` characters. |
| `split(str, delim)` | Split by delimiter. Empty delimiter splits into individual characters. |
| `slice(str, start, end?)` | Extract a portion. Supports negative indices (`-1` = last element). Works on strings and arrays. |

### Replacement

| Function | Description |
|:---------|:----------- |
| `replace(str, old, new)` | Replace all occurrences. |
| `replaceFirst(str, old, new)` | Replace first occurrence only. |

### Type Conversion

| Function | Description |
|:---------|:----------- |
| `toNumber(str)` | Parse to a number. Throws if not parseable. |
| `toBoolean(str)` | `"true"`/`"1"`/`"yes"`/`"on"` → `true`, `"false"`/`"0"`/`"no"`/`"off"`/`""` → `false`. Case-insensitive. |

### Padding

| Function | Description |
|:---------|:----------- |
| `padLeft(str, len, padChar?)` | Left-pad to `len` (default pad: space). No truncation. |
| `padRight(str, len, padChar?)` | Right-pad to `len`. |
| `padBoth(str, len, padChar?)` | Pad both sides. Odd extra goes right. |

### Encoding

| Function | Description |
|:---------|:----------- |
| `urlEncode(str)` | URL-encode via `Uri.EscapeDataString`. |
| `base64Encode(str)` | UTF-8 then Base64. |
| `base64Decode(str)` | Base64 then UTF-8. |

### Examples

```
length("hello")                         → 5
contains("hello world", "world")        → true
startsWith("hello", "he")               → true
trim("  hello  ")                       → "hello"
trim("**hello**", "*")                  → "hello"
toUpper("hello")                        → "HELLO"
toTitle("hello world")                  → "Hello World"
repeat("ha", 3)                         → "hahaha"
reverse("hello")                        → "olleh"
left("hello", 3)                        → "hel"
right("hello", 3)                       → "llo"
split("a,b,c", ",")                     → ["a", "b", "c"]
replace("aaa", "a", "b")                → "bbb"
replaceFirst("aaa", "a", "b")           → "baa"
naturalSort(["f10", "f2", "f1"])        → ["f1", "f2", "f10"]
toNumber("123.4")                       → 123.4
toBoolean("yes")                        → true
padLeft("5", 3, "0")                    → "005"
padBoth("hi", 6, "*")                   → "**hi**"
slice("hello world", 0, 5)              → "hello"
slice("hello world", -5)                → "world"
base64Encode("hello")                   → "aGVsbG8="
coalesce("", null, "found")             → "found"
```

> **Note:** String functions return `undefined` if any required argument is `undefined`, letting you chain them safely with `??`.

## Object Functions

| Function | Description |
|:---------|:----------- |
| `merge(obj1, obj2, ...)` | Shallow merge. Later arguments overwrite earlier keys. |
| `keys(obj)` | Array of keys. |
| `values(obj)` | Array of values. |
| `pick(obj, keys)` | New object with only the listed keys. Keys may be a single string or an array of strings. |
| `omit(obj, keys)` | New object with the listed keys removed. |
| `mapValues(obj, fn)` | New object with `fn(value, key)` applied to each value. |
| `flattenObject(obj, sep?)` | Flatten nested objects to a single level, joining keys with `sep` (default `_`). |

```
merge({a: 1}, {b: 2})                    → {a: 1, b: 2}
merge({a: 1, b: 2}, {b: 3, c: 4})        → {a: 1, b: 3, c: 4}
keys({a: 1, b: 2, c: 3})                 → ["a", "b", "c"]
values({a: 1, b: 2, c: 3})               → [1, 2, 3]
pick({a: 1, b: 2, c: 3}, ["a", "c"])     → {a: 1, c: 3}
omit({a: 1, b: 2, c: 3}, ["b"])          → {a: 1, c: 3}
mapValues({a: 1, b: 2}, (v, k) => v * 10) → {a: 10, b: 20}
```

## Array Literals

```
[1, 2, 3]
[1, 2, 3, 2+2, 10/2, 3!]
[1, ...rest, 5]              // spread
```

## Object Literals

```
{a: 1, b: 2}
{name: "John", age: 30}
{"my-key": 1}                // quoted keys for non-identifier names
{...base, override: true}    // spread
```

## Property Access

```
user.name                               // dot access
user.profile.name                       // chained
items[0]                                // array index
items[0].name                           // combined
user.missing                            // → undefined (not an error)
```

Missing properties and out-of-range array indices return `undefined` instead of throwing. Use `??` for fallbacks (see below).

## Function Definitions

You can define functions using `name(params) = expression`:

```
square(x) = x*x
add(a, b) = a + b
factorial(x) = x < 2 ? 1 : x * factorial(x - 1)
```

These can be passed to higher-order functions:

```
name(u) = u.name; map(users, name)
add(a, b) = a+b; fold([1, 2, 3], 0, add)
```

Inline:

```
filter([1, 2, 3, 4, 5], isEven(x) = x % 2 == 0)
```

### Arrow Functions

Concise syntax for inline functions.

**Single parameter (no parentheses):**

```
map([1, 2, 3], x => x * 2)           → [2, 4, 6]
filter([1, 2, 3, 4], x => x > 2)     → [3, 4]
map(users, x => x.name)
```

**Multiple parameters (parentheses required):**

```
fold([1, 2, 3, 4, 5], 0, (acc, x) => acc + x)    → 15
fold([1, 2, 3, 4, 5], 1, (acc, x) => acc * x)    → 120
map([10, 20, 30], (val, idx) => val + idx)       → [10, 21, 32]
```

**Zero parameters:**

```
(() => 42)()                         → 42
```

**Assignment:**

```
fn = x => x * 2; map([1, 2, 3], fn)  → [2, 4, 6]
double = x => x * 2; triple = x => x * 3
```

**Nested:**

```
map([[1, 2], [3, 4]], row => map(row, x => x * 2))   → [[2, 4], [6, 8]]
```

## Constants

| Constant | Value | Description |
|:---------|:------|:------------|
| `E` | ~2.718 | Euler's number |
| `PI` | ~3.14159 | Ratio of a circle's circumference to its diameter |
| `Infinity` | ∞ | Positive infinity |
| `NaN` | NaN | Not-a-number |
| `true` | true | Logical true |
| `false` | false | Logical false |
| `null` | null | The null value |
| `undefined` | undefined | Missing-value sentinel; distinct from `null` |

## Coalesce Operator (`??`)

Returns the right operand when the left is `null`, `undefined`, `Infinity`, or `NaN`:

```
x ?? 0                   → 0 (if x is undefined or null)
y ?? "default"           → y (if y has a value)
10 / 0                   // throws - see note below
sqrt(-1) ?? 0            → 0 (sqrt of negative is NaN, which is coalesced)
user.nickname ?? user.name ?? "Anonymous"
settings.timeout ?? 5000
```

> **Note on division:** Unlike JavaScript, ExpresZo throws on `x / 0` rather than returning `Infinity`. Use `if(y == 0, fallback, x / y)` if you need a safe-divide.

## Optional Property Access

Property access returns `undefined` when any part of the chain is missing:

```
user.profile.name             → "Ada" (if all parts exist)
user.profile.email            → undefined (if email is missing)
user.settings.theme           → undefined (if settings is missing)
user.settings.theme ?? "dark" → "dark"
items[99].value               → undefined (if index out of range)
items[0].price ?? 0           → 0 (fallback if missing)
```

## CASE Expressions

SQL-style CASE expressions provide multi-way conditionals.

### Switch-style

Compare a value against multiple options:

```
case status
  when "active"   then "✓ Active"
  when "pending"  then "⏳ Pending"
  when "inactive" then "✗ Inactive"
  else "Unknown"
end
```

Comparison uses strict equality (`==`).

### Condition-style (no subject)

Evaluate multiple conditions like if / else if / else:

```
case
  when score >= 90 then "A"
  when score >= 80 then "B"
  when score >= 70 then "C"
  when score >= 60 then "D"
  else "F"
end
```

The first truthy condition wins. If none match and there's no `else`, the result is `undefined`.

### Examples

```
// Categorize a number
case
  when x < 0 then "negative"
  when x == 0 then "zero"
  else "positive"
end

// Map status codes
case code
  when 200 then "OK"
  when 404 then "Not Found"
  when 500 then "Server Error"
  else "Unknown: " | code
end
```

## In and Not In Operators

Check if a value exists in an array:

```
"apple" in ["apple", "banana", "cherry"]       → true
"grape" in ["apple", "banana", "cherry"]       → false
"grape" not in ["apple", "banana", "cherry"]   → true
5 in [1, 2, 3, 4, 5]                           → true
```

Element comparison is strict equality.

## JSON Function

Convert values to JSON strings:

```
json([1, 2, 3])           → "[1,2,3]"
json({a: 1, b: 2})        → '{"a":1,"b":2}'
json("hello")             → '"hello"'
```

Function values and `undefined` are dropped from the output (the latter in objects) or emitted as `null` (in arrays). This matches `JSON.stringify` behaviour.

## Custom Functions

Your application may provide additional custom functions beyond the built-in ones. Check your application's documentation.
