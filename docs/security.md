# Security & Validation

> **Audience:** Developers integrating ExpresZo, especially when expressions come from untrusted input (end users, admin UIs, rule editors).

ExpresZo is designed to be safe to evaluate untrusted expressions. It does this through a small set of guarantees enforced at every user-reachable access point.

## Guarantees

### No arbitrary code execution

Expressions can only invoke the functions registered on the parser - the built-in preset (math, string, array, object, utility, type-check) plus any custom functions your application adds. Raw CLR delegates, `MethodInfo` handles, reflection invocations, `System.Linq.Expressions` compilation, and `DynamicMethod` IL emission are all absent from the evaluator. `Value.Function` wraps an `ExprFunc` delegate pointing into a known implementation - nothing else can be invoked.

### No prototype pollution

Access to `__proto__`, `prototype`, and `constructor` is **always** rejected, via dot access, bracket access, or even naming a variable those names. This matches the TypeScript library's block-list and mitigates the class of attacks that `expr-eval` [CVE-2025-12735](https://github.com/silentmatt/expr-eval/security/advisories) targeted.

```csharp
parser.Evaluate("obj.__proto__");           // throws AccessException
parser.Evaluate("obj[\"__proto__\"]");      // throws AccessException
parser.Evaluate("__proto__");               // throws AccessException
```

### Array index sanity

Bracket access into arrays requires a finite integer index. Non-integer, NaN, and Infinity indices raise `ExpressionArgumentException`. Out-of-range indices return `undefined` rather than throwing - this matches the "optional chaining" behaviour documented in the syntax reference.

### Recursion depth cap

The Pratt parser enforces a hard cap on nesting depth (256) during parsing. A deeply-nested expression that would blow the stack at evaluation time is rejected with a `ParseException` before the AST is even produced.

The evaluator enforces the same 256 cap on nested `Call` depth at runtime (`EvaluationLimits.MaxCallDepth`). Runaway recursion - e.g. `f(x) = f(x); f(1)` - fails with an `EvaluationException` instead of an uncatchable `StackOverflowException`.

### Resource budgets

Allocation-heavy built-ins cap their output so a single expression can't exhaust host memory:

| Limit | Default | Applied to |
|---|---|---|
| `MaxStringLength` | 1,000,000 chars | `repeat`, `padLeft`, `padRight`, `padBoth` |
| `MaxArrayLength` | 1,000,000 items | `range` |
| `MaxFactorialInput` | 170 | `fac`, postfix `!` (beyond this, the result exceeds `double.MaxValue`) |

All three are exposed as constants on `Expreszo.EvaluationLimits` for observability.

### Cancellation

`EvaluateAsync` observes its `CancellationToken` at every `Call` boundary and inside every looping built-in (`filter`, `map`, `fold`, `reduce`, `find`, `some`, `every`, `groupBy`, `countBy`, `sort`, `mapValues`). Long-running expressions will surface `OperationCanceledException` within a bounded number of iterations.

## `ExpressionValidator`

The guarantees above are enforced by `Expreszo.Validation.ExpressionValidator`, a static class the evaluator calls into at every access point. You can call it directly in your own validators, custom functions, or user-extension hooks.

### `DangerousProperties`

The block-list is exposed as a `FrozenSet<string>`:

```csharp
using Expreszo.Validation;

ExpressionValidator.DangerousProperties.Contains("__proto__");  // true
ExpressionValidator.DangerousProperties.Contains("prototype");  // true
ExpressionValidator.DangerousProperties.Contains("constructor"); // true
```

### Validation methods

```csharp
// Throws AccessException on __proto__, prototype, constructor
ExpressionValidator.ValidateVariableName("userInput");
ExpressionValidator.ValidateMemberAccess("userInput");

// Throws ExpressionArgumentException for non-integer, NaN, or Infinity indices
ExpressionValidator.ValidateArrayAccess(parent: Value.Array.Empty, index: Value.Number.Of(3));

// Throws ExpressionArgumentException for null/undefined required args
ExpressionValidator.ValidateRequiredParameter(value, nameof(value));

// Throws FunctionException if value is not a Value.Function
ExpressionValidator.ValidateFunctionCall(value, "max");

// Allow-list check for registered implementations
ExpressionValidator.ValidateAllowedFunction(fn, registeredFunctions);
```

### Function allow-listing

`ValidateAllowedFunction` defends against a scenario where an attacker (or a misconfigured feature) injects a raw `ExprFunc` delegate via a scope binding or custom resolver that points at something it shouldn't. The allow-list check accepts:

- Any `Value.Function` whose `Invoke` delegate is reference-equal to an entry in the registered function table (functions or unary operators).
- Any lambda the evaluator itself produced. These carry an internal `IsExpressionLambda` marker whose setter is `internal init` - callers outside the assembly cannot forge it, so the trust boundary is identity-based, not name-based.

Anything else is rejected with a `FunctionException`. **The check runs on every call site in the evaluator by default** - a resolver returning `new Value.Function(untrustedDelegate)` will be rejected the moment the expression invokes it.

## Exception hierarchy

All ExpresZo exceptions derive from `Expreszo.Errors.ExpressionException` and carry an `ErrorContext` with optional expression text, 1-based position, source span, and names:

```csharp
public abstract class ExpressionException : Exception
{
    public ErrorContext Context { get; }
    public string? Expression => Context.Expression;
    public ErrorPosition? Position => Context.Position;
}
```

| Type | Raised when… |
|:-----|:-------------|
| `ParseException` | Tokeniser or parser encounters malformed input. |
| `EvaluationException` | General evaluation failure (division by zero, cast errors, etc.). |
| `VariableException` | An identifier can't be resolved in any lookup layer. Carries `VariableName`. |
| `FunctionException` | A function name isn't registered, or a called value isn't callable. Carries `FunctionName`. |
| `AccessException` | Member or variable access hits a blocked property. Carries `PropertyName`. |
| `ExpressionArgumentException` | A built-in or custom function got the wrong argument count or type. Carries `FunctionName`, `ArgumentIndex`, `ExpectedType`, `ReceivedType`. |
| `AsyncRequiredException` | Synchronous `Evaluate` was called but the expression requires async evaluation. |

Catching `ExpressionException` covers all of them.

## Pluggable error handling

`IErrorHandler` lets you observe or redirect errors without changing the throw-by-default semantics.

```csharp
public interface IErrorHandler
{
    ErrorDisposition OnParseError(ParseException exception);
    ErrorDisposition OnEvaluationError(ExpressionException exception);
    void OnWarning(string message, ErrorContext context);
}
```

Return one of three dispositions:

```csharp
public abstract record ErrorDisposition
{
    public sealed record Rethrow : ErrorDisposition;                 // re-raise
    public sealed record Substitute(Value Replacement) : ErrorDisposition;
    public sealed record Abort : ErrorDisposition;                   // stop, return Undefined
}
```

The default `ThrowingErrorHandler` (exposed as `ThrowingErrorHandler.Instance`) always returns `Rethrow` for both. Plug in your own by threading it through `EvalContext` when calling the evaluator directly, or when a future release exposes handler configuration on `Parser`.

## Defence-in-depth checklist

If you're exposing ExpresZo to untrusted input, consider the following in addition to what the library does out-of-the-box:

1. **Cap expression length.** Reject input past a reasonable size (say, 10 KB) before parsing.
2. **Cap evaluation time.** Pass a `CancellationToken` with a timeout to `EvaluateAsync`; don't use `Evaluate` for untrusted input unless you're confident every reachable function is bounded.
3. **Review your custom functions.** Any function registered on the parser is callable from expressions. Review them for unintended side effects (file I/O, network calls, heavy CPU work) just like you would an endpoint.
4. **Don't smuggle delegates through the scope.** If you build a `Scope` manually for advanced scenarios, don't place raw `Value.Function` entries pointing at arbitrary CLR methods - wire them as registered functions instead so `ValidateAllowedFunction` can cover them.
5. **Log, don't rethrow blindly.** Catch `ExpressionException` and log the `Expression`, `Position`, and any contextual names; surface a generic error to the caller.

## See Also

- [Parser](parser.md) - thread safety, variable resolution order.
- [Expression](expression.md) - the public evaluation methods.
- [Advanced Features](advanced-features.md) - custom resolvers, async, cancellation.
- [AOT & Trimming](aot-and-trimming.md) - another layer of the library's "no runtime dynamism" promise.
