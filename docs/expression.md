# Expression

> **Audience:** Developers integrating ExpresZo into their .NET projects.

`parser.Parse(string)` returns an `Expression` object. Expressions are similar to compiled functions - call them with different variable bindings to get different results. Instances are immutable and safe to share across threads.

## `Evaluate(JsonDocument? values, VariableResolver? resolver)`

Synchronous evaluation.

```csharp
using Expreszo;
using System.Text.Json;

var parser = new Parser();
var expr = parser.Parse("2 ^ x");

using var doc = JsonDocument.Parse("""{"x":3}""");
var result = expr.Evaluate(doc);
// result is Value.Number(8)
```

If the expression calls any function whose `ValueTask<Value>` is not synchronously completed, `Evaluate` throws `AsyncRequiredException`. Use `EvaluateAsync` in that case.

### With a `VariableResolver`

The optional resolver argument is a per-call resolver. Pass a lambda with signature `(string name) => VariableResolveResult` to provide dynamic values without mutating parser state:

```csharp
var expr = parser.Parse("$user.name");

VariableResolver resolveAlice = name => name == "$user"
    ? new VariableResolveResult.Bound(new Value.Object(FrozenDictionary.ToFrozenDictionary(
        new Dictionary<string, Value> { ["name"] = new Value.String("Alice") }, StringComparer.Ordinal)))
    : VariableResolveResult.NotResolved;

expr.Evaluate(values: null, resolver: resolveAlice);
```

Return values:

- `new VariableResolveResult.Bound(Value value)` - supply the value directly.
- `new VariableResolveResult.Alias(string name)` - redirect to another variable.
- `VariableResolveResult.NotResolved` - fall through to the next resolution layer.

See [Advanced Features - Custom Variable Resolution](advanced-features.md#custom-variable-resolution) for end-to-end examples.

## `EvaluateAsync(JsonDocument? values, VariableResolver? resolver, CancellationToken ct)`

Asynchronous evaluation. Returns `ValueTask<Value>`. Synchronous completions don't allocate a state machine - so for expressions that use only built-in synchronous functions, the cost is effectively the same as `Evaluate`.

```csharp
var result = await expr.EvaluateAsync(doc, ct: cancellationToken);
```

The cancellation token is checked at every function-call boundary, so long-running async user functions can be interrupted cleanly.

## `Simplify(JsonDocument? values)`

Returns a new `Expression` with constant sub-expressions folded to literal nodes. If you pass `values`, any variable reference resolvable from that document is inlined as well. Useful when you want to pre-compute the parts of a formula that don't change.

```csharp
var expr = parser.Parse("x * (y * atan(1))");
using var partial = JsonDocument.Parse("""{"y":4}""");
var simplified = expr.Simplify(partial);

Console.WriteLine(simplified.ToString());
// (x * 3.141592653589793)

using var full = JsonDocument.Parse("""{"x":2}""");
var result = simplified.Evaluate(full);
// Value.Number(6.283185307179586)
```

Simplify is intentionally conservative:

- **Assignment** (`=`) is never folded - it has a side effect on the scope.
- **Short-circuit operators** (`and` / `or` / `&&` / `||`) aren't folded - their evaluation order is significant.
- **Function calls** aren't pre-evaluated (except pure operator functions) because user functions may be non-deterministic.
- **Member access** is folded only when the base expression simplifies to a literal object.

The result is a new `Expression`; the original is untouched.

## `Substitute(string variable, Expression expr)`

## `Substitute(string variable, string expr)`

Returns a new `Expression` with every `Ident` matching `variable` replaced by the given replacement tree. This is function composition - think of it as "inline this expression wherever `variable` appears." Parameter shadowing inside lambdas and function definitions is respected, so `x` inside `(x => x + 1)` is never substituted.

```csharp
var expr = parser.Parse("2 * x + 1");
Console.WriteLine(expr.ToString());                 // (2 * x + 1)

var replaced = expr.Substitute("x", "4 * y");
Console.WriteLine(replaced.ToString());             // (2 * (4 * y) + 1)

using var doc = JsonDocument.Parse("""{"y":3}""");
Console.WriteLine(replaced.Evaluate(doc));          // Value.Number(25)
```

The second overload parses the replacement expression internally using the same parser configuration.

## `Variables(bool withMembers = false)`

Returns every unbound variable the expression references, deduplicated in discovery order.

```csharp
var expr = parser.Parse("x * (y * atan(1))");
expr.Variables();                         // ["x", "y"]

using var partial = JsonDocument.Parse("""{"y":4}""");
expr.Simplify(partial).Variables();       // ["x"]
```

By default, member access chains show only their root - `parser.Parse("x.y.z").Variables()` returns `["x"]`. Pass `withMembers: true` to get the whole dotted chain:

```csharp
parser.Parse("x.y.z").Variables(withMembers: true);
// ["x.y.z"]
```

`Variables` is `Symbols` filtered to exclude names registered as built-in functions or operators.

## `Symbols(bool withMembers = false)`

Returns every identifier the expression references, including built-in functions and operators.

```csharp
var expr = parser.Parse("min(x, y, z)");
expr.Symbols();                                    // ["min", "x", "y", "z"]

using var partial = JsonDocument.Parse("""{"y":4,"z":5}""");
expr.Simplify(partial).Symbols();                  // ["min", "x"]
```

Like `Variables`, `Symbols` accepts `withMembers: true` to group dotted chains.

## `ToString()`

Converts the expression back to source text. Surrounds every sub-expression with parentheses (except literals, variables, and function calls) so precedence is always explicit - handy when diagnosing parse issues.

```csharp
parser.Parse("1 + 2 * 3").ToString();
// (1 + (2 * 3))

parser.Parse("x = 1; x + 2").ToString();
// ((x = 1) ; (x + 2))
```

The result is cached per `Expression` instance, so repeated calls are free.

## `Accept<T>(INodeVisitor<T> visitor)`

Applies a user-supplied visitor to the AST root. Use this to walk the expression tree for analysis, formatting, or validation that isn't covered by the built-in visitors.

```csharp
using Expreszo.Ast;

public sealed class DepthVisitor : NodeVisitor<int>
{
    protected override int VisitDefault(Node node) => 1;
    public override int VisitBinary(Binary node) =>
        1 + Math.Max(Visit(node.Left), Visit(node.Right));
    public override int VisitParen(Paren node) => Visit(node.Inner);
}

var expr = parser.Parse("(1 + 2) * (3 + (4 * 5))");
var depth = expr.Accept(new DepthVisitor());
```

`NodeVisitor<T>` (the concrete base class) dispatches `Visit(Node)` to one of 20 `VisitXxx` methods, falling back to `VisitDefault` for any node you don't override. See the [AST types](https://github.com/pro-fa/expreszo-dotnet/blob/main/src/Expreszo/Ast/Node.cs) for the full node list.

## See Also

- [Parser](parser.md) - creating a parser and invoking it.
- [Expression Syntax](syntax.md) - what can appear in an expression.
- [Advanced Features](advanced-features.md) - async, resolvers, CASE, object literals, type casts.
- [Values & JsonDocument](values-and-json.md) - how `Value` instances map to JSON.
